// ##aspect=AutoComment

using AOP.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aspectimum.Demo.Lib
{

    [AopTemplate("ClassLevelTemplateForMethods", NameFilter = "First")]
    [AopTemplate("StaticAnalyzer", Action = AopTemplateAction.Classes)]
    [AopTemplate("DependencyInjection", AdvicePriority = 500, Action = AopTemplateAction.PostProcessingClasses)]
    [AopTemplate("ResourceReplacer", AdvicePriority = 1000, ExtraTag = "ResourceFile=Demo.resx,ResourceClass=Demo", Action = AopTemplateAction.PostProcessingClasses)]
    public class ConsoleDemo
    {
        public virtual Person FirstDemo(string firstName, string lastName, int age)
        {
            Console.Out.WriteLine("FirstDemo: 1");

            // ##aspect="FirstDemoComment" extra data here

            return new Person()
            {
                FirstName = firstName,
                LastName = lastName,
                Age = age,
            };
        }

        private static IConfigurationRoot _configuration = inject;
        private IDataService _service { get; } = inject;
        private Person _somePerson = inject;

        [AopTemplate("LogExceptionMethod")]
        [AopTemplate("StopWatchMethod")]
        [AopTemplate("MethodFinallyDemo", AdvicePriority = 100)]
        public Customer[] SecondDemo(Person[] people)
        {
            IEnumerable<Customer> Customers;

            Console.Out.WriteLine("SecondDemo: 1");

            Console.Out.WriteLine(i18("SecondDemo: i18"));

            int configDelayMS = inject;
            string configServerName = inject;

            using (new AopTemplate("SecondDemoUsing", extraTag: "test extra"))
            {

                Customers = people.Select(s => new Customer()
                {
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Age = s.Age,
                    Id = s.Id
                });

                _service.Init(Customers);

                foreach (var customer in Customers)
                {
                    Console.Out.WriteLine(i18($"First Name {customer.FirstName} Last Name {customer.LastName}"));
                    Console.Out.WriteLine("SecondDemo: 2 " + i18("First Name ") + customer.FirstName + i18(" Last Name   ") + customer.LastName);
                }
            }

            Console.Out.WriteLine(i18(String.Format("SecondDemo: {0}", "3")));
            Console.Out.WriteLine($"Server {configServerName} default delay {configDelayMS}");
            Console.Out.WriteLine($"Customer for ID=5 is {_service.GetCustomerName(5)}");

            return Customers.ToArray();
        }

        protected static string i18(string s) => s;
        protected static dynamic inject;

        [AopTemplate("NotifyPropertyChangedClass", Action = AopTemplateAction.Classes)]
        [AopTemplate("NotifyPropertyChanged", Action = AopTemplateAction.Properties)]
        public class Person
        {
            [AopTemplate("CacheProperty", extraTag: "{ \"CacheKey\": \"name_of_cache_key\", \"ExpiresInMinutes\": 10 }")]
            public string FullName
            {
                get
                {
                    // ##aspect="FullNameComment" extra data here
                    return $"{FirstName} {LastName}";
                }
            }

            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public int Age { get; set; }
        }

        [AopTemplate("NotifyPropertyChanged", Action = AopTemplateAction.Properties)]
        public class Customer : Person
        {
            public double CreditScore { get; set; }
        }

        public interface IDataService
        {
            void Init(IEnumerable<Customer> customers);
            string GetCustomerName(int customerId);
        }

        public class DataService: IDataService
        {
            private IEnumerable<Customer> _customers;
            public void Init(IEnumerable<Customer> customers)
            {
                _customers = customers;
            }

            public string GetCustomerName(int customerId)
            {
                return _customers.FirstOrDefault(w => w.Id == customerId)?.FullName;
            }
        }

        public class MockDataService : IDataService
        {
            private IEnumerable<Customer> _customers;
            public void Init(IEnumerable<Customer> customers)
            {
                if(customers == null)
                    throw (new Exception("IDataService.Init(customers == null)"));
            }

            public string GetCustomerName(int customerId)
            {
                if (customerId < 0)
                    throw (new Exception("IDataService.GetCustomerName: customerId cannot be negative"));

                if (customerId == 0)
                    throw (new Exception("IDataService.GetCustomerName: customerId cannot be zero"));

                return $"FirstName{customerId} LastName{customerId}";
            }
        }
    }
}
