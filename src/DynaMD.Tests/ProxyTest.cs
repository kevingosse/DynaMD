using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DynaMD.TestChildProcess;
using Microsoft.Diagnostics.Runtime;
using NUnit.Framework;

namespace DynaMD.Tests
{
    public class ProxyTest
    {
        private Process _childProcess;
        private DataTarget _dataTarget;
        private ClrHeap _heap;

        [SetUp]
        public void Dump()
        {
            var processStartInfo = new ProcessStartInfo(typeof(Program).Assembly.Location)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _childProcess = Process.Start(processStartInfo);

            var line = _childProcess.StandardOutput.ReadLine();

            if (line != Program.Ready)
            {
                throw new Exception("Unexpected output from child process: " + line);
            }

            _dataTarget = DataTarget.AttachToProcess(_childProcess.Id, 5000);

            var runtime = _dataTarget.ClrVersions[0].CreateRuntime();

            _heap = runtime.GetHeap();
        }

        [TearDown]
        public void CleanDump()
        {
            _dataTarget?.Dispose();
            _childProcess?.Kill();
        }

        [Test]
        public void Can_read_ref_field_from_class()
        {
            var proxy = GetProxy<ClassWithReference>();

            Assert.AreEqual("OK", proxy.Reference.Value);
        }

        [Test]
        public void Can_read_ref_field_from_struct()
        {
            var proxy = GetProxy<StructWithStringField>();

            Assert.AreEqual("OK", proxy.Value);
        }

        [Test]
        public void Can_read_value_field_from_struct()
        {
            var proxy = GetProxy<StructWithULongField>();

            Assert.AreEqual(666, proxy.Value);
        }

        [Test]
        public void Can_read_value_field_from_class()
        {
            var proxy = GetProxy<ClassWithULongField>();

            Assert.AreEqual(666, proxy.Value);
        }

        [Test]
        public void Can_read_struct_field_from_class()
        {
            var proxy = GetProxy<ClassWithStructField>();

            Assert.AreEqual(666, proxy.Value.Value);
        }

        [Test]
        public void Can_read_struct_field_from_struct()
        {
            var proxy = GetProxy<StructWithStructField>();

            Assert.AreEqual(666, proxy.Value.Value);
        }

        [Test]
        public void Can_read_struct_field_from_nested_struct()
        {
            var proxy = GetProxy<StructWithStructWithStructField>();

            Assert.AreEqual(666, proxy.Value.Value.Value);
        }

        [Test]
        public void Can_read_array_of_struct()
        {
            var proxy = GetProxy<ClassWithArrayOfStruct>();

            Assert.AreEqual(2, proxy.Array[2].Value);
        }

        [Test]
        public void Can_read_array_of_class()
        {
            var proxy = GetProxy<ClassWithArrayOfClass>();

            Assert.AreEqual("2", proxy.Values[2].Value);
        }

        [Test]
        public void Can_foreach_on_an_array()
        {
            var proxy = GetProxy<ClassWithArray>();

            var expected = Enumerable.Range(0, 10).Select(i => 10 - i).ToList();

            var actual = new List<int>();

            foreach (var value in proxy.Values)
            {
                actual.Add(value);
            }

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Can_read_length_of_array()
        {
            var proxy = GetProxy<ClassWithArray>();

            Assert.AreEqual(10, proxy.Values.Length);
        }

        private dynamic GetProxy<T>()
        {
            var address = FindAddress<T>();

            return _heap.GetProxy(address);
        }

        private ulong FindAddress<T>()
        {
            var typeName = typeof(T).FullName;

            return _heap.EnumerateObjectAddresses()
                .First(u => _heap.GetObjectType(u).Name == typeName);
        }
    }
}
