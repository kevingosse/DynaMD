using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DynaMD.TestChildProcess
{
    public class Program
    {
        public const string Ready = "Ready";

        private static void Main(string[] args)
        {
            var values = new object[]
            {
                new ClassWithReference(),
                new ClassWithStringField(),
                new ClassWithStringProperty(),
                new ClassWithULongField(),
                new ClassWithArray(),
                new StructWithStringField("OK"),
                new StructWithULongField(666),
                new ClassWithStructField(),
                new ClassWithArrayOfStruct(),
                new ClassWithArrayOfClass(),
                new StructWithStructField(666),
                new StructWithStructWithStructField(666),
                new ClassWithEmptyArray(),
                new StructWithDate(),
                new StructWithArray(),
                CreateDictionary(),
                new ConcurrentQueue<int>(),
                new ClassWithReference2(),
                new ClassWithListOfString()
            };

            Console.WriteLine(Ready);

            Console.ReadLine();

            GC.KeepAlive(values);
        }

        private static ConcurrentDictionary<int, string> CreateDictionary()
        {
            var dictionary = new ConcurrentDictionary<int, string>();
            dictionary.TryAdd(1, "one");
            dictionary.TryAdd(2, "two");
            return dictionary;
        }
    }

    public class ClassWithListOfString
    {
        public List<string> List = new List<string> { "Hello", "World" };
    }

    public class StructWithDate
    {
        public DateTime Date = new DateTime(2001, 1, 1);
    }

    public struct StructWithArray
    {
        public int[] Values;
    }

    public class ClassWithArray
    {
        public int Field;
        public int Field2;
        public int Field3;
        public int[] Values = Enumerable.Range(0, 10).Select(i => 10 - i).ToArray();
    }

    public class ClassWithEmptyArray
    {
        public int[] Values = new int[0];
    }

    public class ClassWithArrayOfClass
    {
        public int Field = 10;
        public int Field2 = 11;
        public int Field3 = 12;
        public ClassWithStringField[] Values = Enumerable.Range(0, 10).Select(i => new ClassWithStringField { Value = i.ToString() }).ToArray();
    }


    public class ClassWithReference
    {
        public ClassWithStringField Reference = new ClassWithStringField();
    }

    public class ClassWithReference2
    {
        public ClassWithULongField Reference = new ClassWithULongField();
    }

    public class ClassWithStringField
    {
        public string Value = "OK";
    }

    public class ClassWithStringProperty
    {
        public string Field { get; set; }

        public ClassWithStringProperty()
        {
            Field = "OK";
        }
    }

    public class ClassWithULongField
    {
        public ulong Value = 666;
    }

    public struct StructWithStringField
    {
        public string Value;

        public StructWithStringField(string value)
        {
            Value = value;
        }
    }

    public struct StructWithStructWithStructField
    {
        public int Field1;
        public int Field2;
        public StructWithStructField Value;

        public StructWithStructWithStructField(ulong value)
        {
            Field1 = 42;
            Field2 = 43;
            Value = new StructWithStructField(value);
        }
    }

    public struct StructWithStructField
    {
        public int Field1;
        public int Field2;
        public StructWithULongField Value;

        public StructWithStructField(ulong value)
        {
            Field1 = 42;
            Field2 = 43;
            Value = new StructWithULongField(value);
        }
    }

    public struct StructWithULongField
    {
        public int Field;
        public int Field2;
        public int Field3;
        public int Field4;
        public int Field5;
        public ulong Value;

        public StructWithULongField(ulong value)
        {
            Field = 42;
            Field2 = 43;
            Field3 = 44;
            Field4 = 45;
            Field5 = 46;
            Value = value;
        }
    }

    public class ClassWithStructField
    {
        public int Field = 4;
        public int Field2 = 4;
        public StructWithULongField Value = new StructWithULongField(666);
    }

    public class ClassWithArrayOfStruct
    {
        public int Field = 4;
        public int Field2 = 4;
        public StructWithULongField[] Array;

        public ClassWithArrayOfStruct()
        {
            Array = new StructWithULongField[4];

            for (int i = 0; i < 4; i++)
            {
                Array[i] = new StructWithULongField((ulong)i);
            }
        }
    }

}
