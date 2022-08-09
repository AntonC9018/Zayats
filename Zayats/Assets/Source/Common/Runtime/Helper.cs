using System;
using System.Collections.Generic;

namespace Common
{
    public static class HelperThing
    {
        public static T? MaybeFirst<T>(this IEnumerable<T> e, System.Predicate<T> func) where T : struct
        {
            foreach (T el in e)
            {
                if (func(el))
                    return el;
            }
            return null;
        }

        public static List<T> Overwrite<T>(this IEnumerable<T> e, List<T> list)
        {
            list.Clear();
            list.AddRange(e);
            return list;
        }

        public static int IndexOf<T>(this IEnumerable<T> e, System.Predicate<T> func)
        {
            int i = 0;
            foreach (T el in e)
            {
                if (func(el))
                    return i;
                else
                    i++;
            }
            return -1;
        }

        public static int IndexOf<T>(this IEnumerable<T> e, T item) where T : class
        {
            int i = 0;
            foreach (T el in e)
            {
                if (el.Equals(item))
                    return i;
                else
                    i++;
            }
            return -1;
        }

        public static int AddIfContains_RemoveIfNot<T>(this List<T> list, T item)
        {
            int index = list.IndexOf(item);
            if (index == -1)
                list.Add(item);
            else
                list.RemoveAt(index);
            return index;
        }

        // public static int Count<T>(this Span<T> span, System.Predicate<T> func)
        // {
        //     int a = 0;
        //     for (int i = 0; i < span.Length; i++)
        //     {
        //         if (func(span[i]))
        //             a++;
        //     }
        //     return a;
        // }

        // public static List<T> EagerWhere<T>(this Span<T> span, System.Predicate<T> func)
        // {
        //     List<T> a = new();
        //     EagerWhere(span, func, a);
        //     return a;
        // }

        // public static void EagerWhere<T>(this Span<T> span, System.Predicate<T> func, List<T> outResult)
        // {
        //     for (int i = 0; i < span.Length; i++)
        //     {
        //         if (func(span[i]))
        //             outResult.Add(span[i]);
        //     }
        // }


        public static int Count<T>(this ArraySegment<T> span, System.Predicate<T> func)
        {
            int a = 0;
            for (int i = 0; i < span.Count; i++)
            {
                if (func(span[i]))
                    a++;
            }
            return a;
        }

        public static List<int> Indices<T>(this ArraySegment<T> span, System.Predicate<T> func)
        {
            List<int> a = new();
            span.Indices(func, a);
            return a;
        }

        public static void Indices<T>(this ArraySegment<T> span, System.Predicate<T> func, List<int> outResult)
        {
            for (int i = 0; i < span.Count; i++)
            {
                if (func(span[i]))
                    outResult.Add(i);
            }
        }

        public static List<T> EagerWhere<T>(this ArraySegment<T> span, System.Predicate<T> func)
        {
            List<T> a = new();
            span.EagerWhere(func, a);
            return a;
        }

        public static void EagerWhere<T>(this ArraySegment<T> span, System.Predicate<T> func, List<T> outResult)
        {
            for (int i = 0; i < span.Count; i++)
            {
                if (func(span[i]))
                    outResult.Add(span[i]);
            }
        }
    }
}