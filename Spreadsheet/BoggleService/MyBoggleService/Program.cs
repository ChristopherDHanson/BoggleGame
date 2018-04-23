// Authors Bryce Hansen, Christopher Hanson for CS 3500, April 23, 2018

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CustomNetworking;

namespace MyBoggleService
{
    class Program
    {
        static void Main()
        {
            SSListener server = new SSListener(60000, Encoding.UTF8);
            server.Start();
            server.BeginAcceptSS(ConnectionRequested, server);

            Console.ReadLine();
        }

        /// <summary>
        /// Simple method that sets up the StringSocket and its listener.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="payload"></param>
        private static void ConnectionRequested(SS s, object payload)
        {
            SSListener server = (SSListener) payload;
            server.BeginAcceptSS(ConnectionRequested, server);
            new BoggleSocket(s);
        }
    }
}
