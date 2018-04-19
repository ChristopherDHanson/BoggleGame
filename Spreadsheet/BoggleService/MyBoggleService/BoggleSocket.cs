using CustomNetworking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyBoggleService
{
    class BoggleSocket
    {
        private SSListener server;
        private BoggleService BService;

        private List<SS> clients;

        private readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        public BoggleSocket(int port)
        {
            BService = new BoggleService();
            clients = new List<SS>();

            Encoding e = Encoding.UTF8;
            server = new SSListener(port, e);
            server.Start();

            server.BeginAcceptSS(ConnectionRequested, null);
        }

        /// <summary>
        /// This is the callback method that is passed to BeginAcceptSocket.  It is called
        /// when a connection request has arrived at the server.
        /// </summary>
        private void ConnectionRequested(SS s, object payload)
        {
            // We obtain the socket corresonding to the connection request.  Notice that we
            // are passing back the IAsyncResult object.
            SS s2 = s;

            // We ask the server to listen for another connection request.  As before, this
            // will happen on another thread.
            server.BeginAcceptSS(ConnectionRequested, null);

            // We create a new ClientConnection, which will take care of communicating with
            // the remote client.  We add the new client to the list of clients, taking 
            // care to use a write lock.
            try
            {
                sync.EnterWriteLock();
                clients.Add(s);
                s.BeginReceive(CallRequest, null);
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        private void CallRequest (string requestStr, object payload)
        {
            // Parse thru the requestStr, get relevant data
            StringReader reader = new StringReader(requestStr);
            string line = reader.Read();
        }
    }
}
