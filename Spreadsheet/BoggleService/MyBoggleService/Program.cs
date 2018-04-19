using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyBoggleService
{
    class Program
    {
        static void Main()
        {
            HttpStatusCode status;
            UserName name = new UserName { Nickname = "Joe" };
            BoggleSocket service = new BoggleSocket(60000);
//            Token user = service.Register(name, out status);
//            Console.WriteLine(user.UserToken);
//            Console.WriteLine(status.ToString());

            // This is our way of preventing the main thread from
            // exiting while the server is in use
            Console.ReadLine();
        }
    }
}
