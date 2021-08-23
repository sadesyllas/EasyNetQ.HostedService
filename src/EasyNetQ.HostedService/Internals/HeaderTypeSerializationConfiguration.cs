using System;
using System.Collections.Generic;

namespace EasyNetQ.HostedService.Internals
{
    public class HeaderTypeSerializationConfiguration
    {
        public string TypeHeader { get; set; }
        
        public Dictionary<string, Type> TypeMappings { get; set; }
    }
}