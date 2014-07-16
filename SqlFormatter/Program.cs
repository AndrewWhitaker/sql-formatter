namespace SqlFormatter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    class Program
    {
        static void Main(string[] args)
        {
            string result = SqlFormatter.Format(@"SELECT top (3)
  customer_id, 
  customer_name
FROM 
    Customer
ORDER BY 
  COUNT(order_id) DESC;");

            Console.WriteLine(result);
            Console.ReadKey();
        }
    }
}
