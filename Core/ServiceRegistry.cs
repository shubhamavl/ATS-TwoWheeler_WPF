using System;
using System.Collections.Concurrent;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.Core
{
    /// <summary>
    /// Lightweight service registry for dependency management
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly ConcurrentDictionary<Type, object> _services = new();

        /// <summary>
        /// Register a service instance
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// Get a service instance
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new Exception($"Service {typeof(T).Name} not registered.");
        }

        /// <summary>
        /// Initialize default services
        /// </summary>
        public static void InitializeDefaultServices()
        {
            // Settings
            Register<ISettingsService>(SettingsManager.Instance);
            
            // CAN Service
            var canService = new CANService();
            Register<ICANService>(canService);
            
            // Weight Processor
            var weightProcessor = new WeightProcessor();
            Register<IWeightProcessorService>(weightProcessor);
            
            // Data Logger
            Register<IDataLoggerService>(new DataLogger());

            // Production Logger
            Register<IProductionLoggerService>(ProductionLogger.Instance);

            // Navigation
            Register<INavigationService>(new NavigationService());

            // Dialog Service
            Register<IDialogService>(new DialogService());
        }
        
        /// <summary>
        /// Cleanup all services
        /// </summary>
        public static void Cleanup()
        {
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _services.Clear();
        }
    }
}
