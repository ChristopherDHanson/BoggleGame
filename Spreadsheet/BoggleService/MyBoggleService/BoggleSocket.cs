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
            string line;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomNetworking;

namespace MyBoggleService
{
    class BoggleSocket
    {
        //class variables
        // Listens for incoming connection requests
        private TcpListener server;

        // All the clients that have connected but haven't closed
        private List<SS> clients = new List<SS>();

        // Read/write lock to coordinate access to the clients list
        private readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        private BoggleService BService;

        public BoggleSocket(int port)
        {
            // A TcpListener listens for incoming connection requests
            server = new TcpListener(IPAddress.Any, port);

            // Start the TcpListener
            server.Start();

            BService = new BoggleService();

            // Ask the server to call ConnectionRequested at some point in the future when 
            // a connection request arrives.  It could be a very long time until this happens.
            // The waiting and the calling will happen on another thread.  BeginAcceptSocket 
            // returns immediately, and the constructor returns to Main.
//            server.BeginAcceptSocket(ConnectionRequested, null);
        }

        //constructor

        //        /// <summary>
        //        /// Sends back index.html as the response body.
        //        /// </summary>
        //        [WebGet(UriTemplate = "/api")]
        //        Stream API();
        private Stream API(out HttpStatusCode status)
        {
            return BService.API(out status);
        }

        //        /// <summary>
        //        /// Registers a new user.
        //        /// If either user.Name or user.Email is null or is empty after trimming, responds with status code Forbidden.
        //        /// Otherwise, creates a user, returns the user's token, and responds with status code Created. 
        //        /// </summary>
        //        [WebInvoke(Method = "POST", UriTemplate = "/users")]
        //        Token Register(UserName user);
        private Token Register(UserName user, out HttpStatusCode status)
        {
            return BService.Register(user, out status);
        }


        //        /// <summary>
        //        /// Joins game. 
        //        /// </summary>
        //        [WebInvoke(Method = "POST", UriTemplate = "/games")]
        //        GameIDOnly Join(TokenTime user);
        private GameIDOnly Join(TokenTime user, out HttpStatusCode status)
        {
            return BService.Join(user, out status);
        }

        //        /// <summary>
        //        /// Cancels join
        //        /// </summary>
        //        [WebInvoke(Method = "PUT", UriTemplate = "/games")]
        //        void CancelJoin(Token user);
        private void CancelJoin(Token user, out HttpStatusCode status)
        {
            BService.CancelJoin(user, out status);
        }


        //        /// <summary>
        //        /// Plays a word
        //        /// </summary>
        //        [WebInvoke(Method = "PUT", UriTemplate = "/games/{GameID}")]
        //        ScoreOnly PlayWord(TokenWord word, string GameID);
        private ScoreOnly PlayWord(TokenWord word, string GameID, out HttpStatusCode status)
        {
            return BService.PlayWord(word, GameID, out status);
        }


        //        /// <summary>
        //        /// Gets game status update
        //        /// </summary>
        //        [WebGet(UriTemplate = "/games/{GameID}?brief={isBrief}")]
        //        GameStatus GetStatus(string GameID, string isBrief);
        private GameStatus GetStatus(string GameID, string isBrief, out HttpStatusCode status)
        { 
            return BService.GetStatus(GameID, isBrief, out status);
        }
    }
}
