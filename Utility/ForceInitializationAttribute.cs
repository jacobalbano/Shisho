using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Utility
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ForceInitializationAttribute : Attribute
    {
        public static void DiscoverAndInitialize(ServiceProvider services)
        {
            foreach (var t in typeof(ForceInitializationAttribute)
                .Assembly.GetExportedTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.GetCustomAttribute<ForceInitializationAttribute>() != null))
                services.GetRequiredService(t);
        }
    }
}
