using DynaMD.TestChildProcess;
using Microsoft.Diagnostics.Runtime;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

            _heap = runtime.Heap;
        }

        [TearDown]
        public void CleanDump()
        {
            _dataTarget?.Dispose();
            _childProcess?.Kill();
        }

        [Test]
        public void Can_marshal_datetime()
        {
            var proxy = GetProxy<StructWithDate>().Date;

            var dt = (DateTime)proxy;

            Assert.AreEqual(new DateTime(2001, 1, 1), dt);
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
        public void Can_cast_to_string()
        {
            var proxy = GetProxy<ClassWithStringField>();

            ClrType type = proxy.GetClrType();

            var address = type.GetFieldByName("Value").GetAddress((ulong)proxy);

            ulong stringAddress;

            _heap.ReadPointer(address, out stringAddress);

            Assert.AreEqual("OK", (string)_heap.GetProxy(stringAddress));
        }

        [Test]
        public void Can_marshal_to_blittable_struct()
        {
            var proxy = GetProxy<StructWithULongField>();

            var value = (StructWithULongField)proxy;

            Assert.AreEqual(666, value.Value);
        }

        [Test]
        public void Can_not_marshal_to_struct_with_array()
        {
            var proxy = GetProxy<StructWithArray>();

            Assert.Throws<InvalidCastException>(() => GC.KeepAlive((StructWithArray)proxy));
        }

        [Test]
        public void Can_marshal_to_array_of_primitives()
        {
            var proxy = GetProxy<ClassWithArray>();

            var value = (int[])proxy.Values;

            Assert.AreEqual(Enumerable.Range(0, 10).Select(i => 10 - i).ToArray(), value);
        }

        [Test]
        public void Can_fetch_value_of_a_concurrentdictionary_bucket()
        {
            var dico = GetProxy<System.Collections.Concurrent.ConcurrentDictionary<int, string>>();
            var buckets = dico.m_tables.m_buckets;

            dynamic bucket = null;

            foreach (var b in buckets)
            {
                if (b != null)
                {
                    bucket = b;
                    break;
                }
            }

            var value = bucket.m_value;

            Assert.IsInstanceOf<string>(value);
        }

        [Test]
        public void Does_not_throw_when_accessing_null_field()
        {
            var queue = GetProxy<System.Collections.Concurrent.ConcurrentQueue<int>>();
            var segment = queue.m_head;
            segment = segment.m_next;

            Assert.IsNull(segment);
        }

        [Test]
        public void Should_not_fix_good_names()
        {
            var typeName = "System.Collections.Concurrent.ConcurrentDictionary<System.Int32,System.String>";

            var fixedName = Extensions.FixTypeName(typeName);

            Assert.AreEqual(typeName, fixedName);
        }

        [Test]
        public void Can_marshal_to_array_of_blittable_struct()
        {
            var proxy = GetProxy<ClassWithArrayOfStruct>();

            var value = (StructWithULongField[])proxy.Array;

            Assert.AreEqual(4, value.Length);

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(i, value[i].Value);
            }
        }

        [Test]
        public void Can_marshal_to_empty_array()
        {
            var proxy = GetProxy<ClassWithEmptyArray>();

            var value = (int[])proxy.Values;

            Assert.AreEqual(new int[0], value);
        }

        [Test]
        public void Can_marshal_struct_field_from_nested_struct_to_blittable_type()
        {
            var proxy = GetProxy<StructWithStructWithStructField>();

            var value = (StructWithULongField)proxy.Value.Value;

            Assert.AreEqual(666, value.Value);
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

        [Test]
        public void Can_read_the_address_of_an_object()
        {
            var containerAddress = _heap.EnumerateObjectAddresses()
                .First(u => _heap.GetObjectType(u).Name == typeof(ClassWithReference).FullName);

            var type = _heap.GetObjectType(containerAddress);
            var field = type.GetFieldByName("Reference");
            var expected = field.GetValue(containerAddress);

            var proxy = GetProxy<ClassWithReference>();

            Assert.AreEqual(expected, (ulong)proxy.Reference);
        }

        [Test]
        public void Can_read_the_type_of_an_object()
        {
            var containerAddress = _heap.EnumerateObjectAddresses()
                .First(u => _heap.GetObjectType(u).Name == typeof(ClassWithReference).FullName);

            var expectedType = _heap.GetObjectType(containerAddress);

            var proxy = GetProxy<ClassWithReference>();

            Assert.AreEqual(expectedType, proxy.GetClrType());
        }

        [Test]
        public void Can_compare_the_typeof_an_object()
        {
            var proxy = GetProxy<ClassWithReference>();

            Assert.IsTrue(proxy.Is(typeof(ClassWithReference).FullName));
        }

        [Test]
        public void Can_find_instances_of_a_type()
        {
            var expectedNumber = _heap.EnumerateObjects().Count(o => o.Type.Name == typeof(ClassWithStringField).FullName);

            var proxies = _heap.GetProxies<ClassWithStringField>().ToList();

            Assert.AreEqual(expectedNumber, proxies.Count);
            Assert.IsTrue(proxies.All(p => p.GetClrType().Name == typeof(ClassWithStringField).FullName));
        }

        [Test]
        public void Can_convert_a_ClrObject()
        {
            var obj = _heap.EnumerateObjects().First(o => o.Type.Name == typeof(ClassWithReference).FullName);

            var proxy = obj.AsDynamic();

            Assert.AreEqual("OK", proxy.Reference.Value);
        }

        private dynamic GetProxy<T>()
        {
            return _heap.GetProxies<T>().First();
        }
    }
}
