using System;

namespace EdjCase.JsonRpc.Router.Swagger
{
    internal class ReflectedControllerDescriptor
    {
        private Type type;

        public ReflectedControllerDescriptor(Type type)
        {
            this.type = type;
        }
    }
}