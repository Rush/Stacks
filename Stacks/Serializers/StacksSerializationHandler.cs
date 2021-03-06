﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks
{
    public class StacksSerializationHandler
    {
        private Dictionary<int, Action<MemoryStream>> serializerHandlerByTypeCode;

        private IMessageHandler messageHandler;
        private IStacksSerializer serializer;
        private IMessageClient client;

        public StacksSerializationHandler(IMessageClient client, IStacksSerializer serializer, IMessageHandler messageHandler)
        {
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNull(serializer, "serializer");
            Ensure.IsNotNull(messageHandler, "messageHandler");

            this.client = client;
            this.serializerHandlerByTypeCode = new Dictionary<int, Action<MemoryStream>>();
            this.messageHandler = messageHandler;
            this.serializer = serializer;

            this.serializer.Initialize();

            ParseMessageHandler();
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            this.serializer.Serialize(obj, ms);
        }

        public void Deserialize(int typeCode, MemoryStream ms)
        {
            Action<MemoryStream> handler;

            if (serializerHandlerByTypeCode.TryGetValue(typeCode, out handler))
            {
                handler(ms);
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("No registered message handler for type code {0}", typeCode));
            }
        }

      

        private void ParseMessageHandler()
        {
            this.serializerHandlerByTypeCode = messageHandler.GetType()
                          .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                          .Where(IsValidMessageHandlerMethod)
                          .Select(mi => Tuple.Create(GetTypeCode(mi), GetMessageHandlerParameterType(mi), mi))
                          .Select(t => Tuple.Create(t.Item1, CreateSerializerForType(t.Item2, t.Item3)))
                          .ToDictionary(t => t.Item1, t => t.Item2);
        }

        private static bool IsValidMessageHandlerMethod(MethodInfo mi)
        {
            if (!HasMessageHandlerAttribute(mi))
                return false;

            if (mi.ReturnType != typeof(void))
                return false;

            if (mi.IsConstructor)
                return false;

            if (mi.GetParameters().Length != 2)
                return false;

            if (!typeof(IMessageClient).IsAssignableFrom(mi.GetParameters()[0].ParameterType))
                return false;

            return true;
        }

        private static bool HasMessageHandlerAttribute(MethodInfo mi)
        {
            return mi.GetCustomAttribute<MessageHandlerAttribute>(true) != null;
        }

        private static int GetTypeCode(MethodInfo mi)
        {
            return mi.GetCustomAttribute<MessageHandlerAttribute>().TypeCode;
        }

        private static Type GetMessageHandlerParameterType(MethodInfo mi)
        {
            return mi.GetParameters()[1].ParameterType;
        }

        private Action<MemoryStream> CreateSerializerForType(Type type, MethodInfo mi)
        {
            var createLambdaMethod = typeof(StacksSerializationHandler)
                                         .GetMethod("CreateLambda", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .MakeGenericMethod(mi.GetParameters()[0].ParameterType, type);

            this.serializer
                .GetType()
                .GetMethod("PrepareSerializerForType")
                .MakeGenericMethod(type)
                .Invoke(this.serializer, Type.EmptyTypes);
                    

            return (Action<MemoryStream>)createLambdaMethod.Invoke(this, new[] { mi });
        }

        private Action<MemoryStream> CreateLambda<T, Y>(MethodInfo mi)
        {
            var handler = (Action<T, Y>)Delegate.CreateDelegate(typeof(Action<T, Y>), messageHandler, mi);

            var deserializer = this.serializer.CreateDeserializer<Y>();

            return ms =>
            {
                var obj = deserializer(ms);
                handler((T)client, obj);
            };
        }
    }
}
