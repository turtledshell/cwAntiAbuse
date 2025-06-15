using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

public static class ReflectionHelper {
    public static class Field {
        private static Dictionary<Type, Dictionary<string, List<FieldPair>>> cachedFields = new Dictionary<Type, Dictionary<string, List<FieldPair>>>();

        private static FieldInfo TryRetrieveFieldFromDict(Type type, string fieldName, BindingFlags flags) {
            if (cachedFields.ContainsKey(type)) {
                Dictionary<string, List<FieldPair>> selectedTypeDict = cachedFields[type];
                if (selectedTypeDict.ContainsKey(fieldName)) {
                    foreach (FieldPair pair in selectedTypeDict[fieldName]) {
                        if (pair.GetFlags() == flags) {
                            return pair.GetField();
                        }
                    }
                }
            }
            return null;
        }
        private static void AddFieldToDict(Type type, string fieldName, BindingFlags flags, FieldInfo field) {
            Trace.WriteLine("[ReflectionHelper] Added Field: " + field);
            if (!cachedFields.ContainsKey(type)) {
                cachedFields[type] = new Dictionary<string, List<FieldPair>>();
            }
            if (!cachedFields[type].ContainsKey(fieldName)) {
                cachedFields[type][fieldName] = new List<FieldPair>();
            }
            cachedFields[type][fieldName].Add(new FieldPair(flags, field));
        }
        public static FieldInfo GetField<T>(string fieldName, bool isInstance = true, BindingFlags flags = BindingFlags.NonPublic) {
            if (flags == BindingFlags.NonPublic) {
                if (isInstance) {
                    flags = BindingFlags.NonPublic | BindingFlags.Instance;
                } else {
                    flags = BindingFlags.NonPublic | BindingFlags.Static;
                }
            }
            FieldInfo field = TryRetrieveFieldFromDict(typeof(T), fieldName, flags);
            if (field == null) {
                field = typeof(T).GetField(fieldName, flags);
                AddFieldToDict(typeof(T), fieldName, flags, field);
            }
            return field;
        }
        public static object GetValue<T>(T instance, string fieldName, bool isInstance = false, BindingFlags flags = BindingFlags.NonPublic) {
            FieldInfo field = GetField<T>(fieldName, isInstance, flags);
            return field.GetValue(instance);
        }
        public static void SetValue<T>(T instance, string fieldName, object value, bool isInstance = false, BindingFlags flags = BindingFlags.NonPublic) {
            FieldInfo field = GetField<T>(fieldName, isInstance, flags);
            field.SetValue(instance, value);
        }
        public class FieldPair {
            private BindingFlags flags;
            private FieldInfo field;

            public FieldPair(BindingFlags Flags, FieldInfo Field) {
                flags = Flags;
                field = Field;
            }
            public BindingFlags GetFlags() {
                return flags;
            }
            public FieldInfo GetField() {
                return field;
            }
        }
    }

    public static class Method {
        private static Dictionary<Type, Dictionary<string, List<MethodPair>>> cachedMethods = new Dictionary<Type, Dictionary<string, List<MethodPair>>>();

        private static MethodInfo TryRetrieveMethodFromDict(Type type, string methodName, BindingFlags flags, Type[] parameters) {
            if (cachedMethods.ContainsKey(type)) {
                Dictionary<string, List<MethodPair>> selectedTypeDict = cachedMethods[type];
                if (selectedTypeDict.ContainsKey(methodName)) {
                    foreach (MethodPair pair in selectedTypeDict[methodName]) {
                        Type[] param = pair.GetParams();
                        if (pair.GetFlags().Equals(flags)) {
                            for (int t = 0; t < parameters.Length; t++) {
                                if (parameters[t] != param[t]) {
                                    continue;
                                }
                            }
                            return pair.GetMethod();
                        }
                    }
                }
            }
            return null;
        }

        private static void AddMethodToDict(Type type, string methodName, BindingFlags flags, Type[] parameters, MethodInfo method) {
            Trace.WriteLine("[ReflectionHelper] Added Method: " + methodName);
            if (!cachedMethods.ContainsKey(type)) {
                cachedMethods[type] = new Dictionary<string, List<MethodPair>>();
            }
            if (!cachedMethods[type].ContainsKey(methodName)) {
                cachedMethods[type][methodName] = new List<MethodPair>();
            }
            cachedMethods[type][methodName].Add(new MethodPair(flags, parameters, method));
        }

        public static MethodInfo GetMethod<T>(string methodName, bool isInstance = true, BindingFlags flags = BindingFlags.NonPublic, Type[] parameters = null) {
            if (flags == BindingFlags.NonPublic) {
                if (isInstance) {
                    flags = BindingFlags.NonPublic | BindingFlags.Instance;
                } else {
                    flags = BindingFlags.NonPublic | BindingFlags.Static;
                }
            }
            if (parameters == null) {
                parameters = new Type[] { };
            }
            MethodInfo method = TryRetrieveMethodFromDict(typeof(T), methodName, flags, parameters);
            if (method == null) {
                method = typeof(T).GetMethod(methodName, flags);
                AddMethodToDict(typeof(T), methodName, flags, parameters, method);
            }
            return method;
        }
        public static object Invoke<T>(T instance, string methodName, bool isInstance = false, BindingFlags flags = BindingFlags.NonPublic, Type[] parameterTypes = null, object[] parameters = null) {
            MethodInfo method = GetMethod<T>(methodName, isInstance, flags, parameterTypes);
            object result = method.Invoke(instance, parameters);
            return result;
        }

        public class MethodPair {
            private BindingFlags flags;
            private Type[] parameters;
            private MethodInfo method;

            public MethodPair(BindingFlags Flags, Type[] Parameters, MethodInfo Method) {
                flags = Flags;
                method = Method;
                parameters = Parameters;
            }
            public BindingFlags GetFlags() {
                return flags;
            }
            public MethodInfo GetMethod() {
                return method;
            }
            public Type[] GetParams() {
                return parameters;
            }
        }
    }
}
