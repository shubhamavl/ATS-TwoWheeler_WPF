using System;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ATS_TwoWheeler_WPF.Core
{
    /// <summary>
    /// Lightweight service registry bridge for gradual DI migration.
    /// This now wraps the Microsoft.Extensions.DependencyInjection provider.
    /// </summary>
    public static class ServiceRegistry
    {
        private static IServiceProvider? _provider;

        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Get a service instance
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("ServiceRegistry provider has not been initialized. Call SetProvider first.");
            }
            
            return _provider.GetRequiredService<T>();
        }
        
        /// <summary>
        /// DEPRECATED: Does nothing now as initialization is handled by App.xaml.cs
        /// </summary>
        public static void InitializeDefaultServices()
        {
            // No-op
        }

        public static void Cleanup()
        {
            // Handled by DI container disposal if needed
        }
    }
}
