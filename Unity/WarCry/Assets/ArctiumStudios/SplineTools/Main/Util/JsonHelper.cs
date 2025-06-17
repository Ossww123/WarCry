using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            var wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(json);
            return wrapper.items;
        }

        public static string ToJson<T>(T[] array)
        {
            var wrapper = new ArrayWrapper<T>();
            wrapper.items = array;
            return JsonUtility.ToJson(wrapper);
        }

        [Serializable]
        private class ArrayWrapper<T>
        {
            public T[] items;
        }
    }
}