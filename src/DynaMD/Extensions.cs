using DynaMD;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Diagnostics.Runtime
{
    public static class Extensions
    {
        public static dynamic GetProxy(this ClrHeap heap, ulong address)
        {
            if (address == 0)
            {
                return null;
            }

            return new DynamicProxy(heap, address);
        }

        public static IEnumerable<dynamic> GetProxies<T>(this ClrHeap heap)
        {
            return GetProxies(heap, typeof(T).FullName);
        }

        public static IEnumerable<dynamic> GetProxies(this ClrHeap heap, string typeName)
        {
            typeName = ToClrMDTypeName(typeName);

            foreach (var address in heap.EnumerateObjectAddresses())
            {
                if (heap.GetObjectType(address)?.Name == typeName)
                {
                    yield return heap.GetProxy(address);
                }
            }
        }

        public static dynamic AsDynamic(this ClrObject clrObject)
        {
            var heap = clrObject.Type.Heap;

            return heap.GetProxy(clrObject.Address);
        }

        public static string ToClrTypeName(string clrMdTypeName)
        {
            var match = Regex.Match(clrMdTypeName, "(?<type>[^<>]+)<(?<generics>.+)>(?<suffix>[\\[\\]]*)");

            if (!match.Success)
            {
                return clrMdTypeName;
            }

            var generics = match.Groups["generics"].Value;
            var type = match.Groups["type"].Value;
            var suffix = match.Groups["suffix"].Value;

            // Need to find the number of generic arguments, without drilling into nested arguments
            int level = 0;

            var genericTypes = new List<string>();

            var currentGenericType = new StringBuilder();

            for (int i = 0; i < generics.Length; i++)
            {
                var ch = generics[i];

                if (ch == ',')
                {
                    if (level == 0)
                    {
                        genericTypes.Add(currentGenericType.ToString());
                        currentGenericType.Clear();
                        continue;
                    }
                }

                if (ch == '<')
                {
                    level++;
                }

                if (ch == '>')
                {
                    level--;
                }

                currentGenericType.Append(ch);
            }

            if (currentGenericType.Length > 0)
            {
                genericTypes.Add(currentGenericType.ToString());
            }

            if (genericTypes.Count > 0)
            {
                var index = type.IndexOf('+');

                if (index == -1)
                {
                    index = type.Length;
                }

                type = type.Insert(index, $"`{genericTypes.Count}");
                type += $"[{string.Join(",", genericTypes.Select(t => $"[{ToClrTypeName(t)}]"))}]";
            }

            return type + suffix;
        }

        public static string ToClrMDTypeName(string clrTypeName)
        {
            if (!clrTypeName.Contains("`"))
            {
                return clrTypeName;
            }

            var sb = new StringBuilder();

            FixGenericsWorker(clrTypeName, 0, clrTypeName.Length, sb);

            return sb.ToString();
        }

        /// <summary>
        /// A messy version with better performance that doesn't use regular expression.
        /// </summary>
        private static int FixGenericsWorker(string name, int start, int end, StringBuilder sb)
        {
            int num1 = 0;
            for (; start < end; ++start)
            {
                char ch = name[start];
                if (ch != '`')
                {
                    if (ch == '[')
                    {
                        ++num1;
                    }

                    if (ch == ']')
                    {
                        --num1;
                    }

                    if (num1 < 0)
                    {
                        return start + 1;
                    }

                    if (ch == ',' && num1 == 0)
                    {
                        return start;
                    }

                    sb.Append(ch);
                }
                else
                {
                    break;
                }
            }
            if (start >= end)
            {
                return start;
            }

            ++start;
            int num2 = 0;
            bool flag1;
            do
            {
                int num3 = 0;
                flag1 = false;
                for (; start < end; ++start)
                {
                    char ch = name[start];
                    if (ch >= '0' && ch <= '9')
                    {
                        num3 = num3 * 10 + (int)ch - 48;
                    }
                    else
                    {
                        break;
                    }
                }
                num2 += num3;
                if (start >= end)
                {
                    return start;
                }

                if (name[start] == '+')
                {
                    for (; start < end && name[start] != '['; ++start)
                    {
                        if (name[start] == '`')
                        {
                            ++start;
                            flag1 = true;
                            break;
                        }
                        sb.Append(name[start]);
                    }
                    if (start >= end)
                    {
                        return start;
                    }
                }
            }
            while (flag1);
            if (name[start] == '[')
            {
                sb.Append('<');
                ++start;
                while (num2-- > 0)
                {
                    if (start >= end)
                    {
                        return start;
                    }

                    bool flag2 = false;
                    if (name[start] == '[')
                    {
                        flag2 = true;
                        ++start;
                    }
                    start = FixGenericsWorker(name, start, end, sb);
                    if (start < end && name[start] == '[')
                    {
                        ++start;
                        if (start >= end)
                        {
                            return start;
                        }

                        sb.Append('[');
                        for (; start < end && name[start] == ','; ++start)
                        {
                            sb.Append(',');
                        }

                        if (start >= end)
                        {
                            return start;
                        }

                        if (name[start] == ']')
                        {
                            sb.Append(']');
                            ++start;
                        }
                    }
                    if (flag2)
                    {
                        while (start < end && name[start] != ']')
                        {
                            ++start;
                        }

                        ++start;
                    }
                    if (num2 > 0)
                    {
                        if (start >= end)
                        {
                            return start;
                        }

                        sb.Append(',');
                        ++start;
                        if (start >= end)
                        {
                            return start;
                        }

                        if (name[start] == ' ')
                        {
                            ++start;
                        }
                    }
                }
                sb.Append('>');
                ++start;
            }
            if (start + 1 >= end || (name[start] != '[' || name[start + 1] != ']'))
            {
                return start;
            }

            sb.Append("[]");
            return start;
        }
    }
}