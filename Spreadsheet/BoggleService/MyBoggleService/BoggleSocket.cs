using CustomNetworking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            HttpStatusCode status;
            StringReader reader = new StringReader(requestStr);
            string line = reader.ReadLine();

            if (line.StartsWith("GET"))
            {
                if (line.Contains(""))
                {
                    GetStatus("", "", out status);
                }
            }
            else if (line.StartsWith("POST"))
            {
                string[] splitLine = line.Split('/');
                if (splitLine[2].Equals("users"))
                {
                    //string gameID = splitLine[3];
                    while (!line.StartsWith("Content-Length:"))
                        reader.ReadLine();
                    splitLine = line.Split(':');
                    int contentLength;
                    if (Int32.TryParse(splitLine[1], out contentLength))
                    {
                        while (!line.Contains("Nickname"))
                            reader.ReadLine();
                        splitLine = line.Split(':');
                        string nickname = splitLine[1];
                        UserName nameToPassIn = new UserName();
                        nameToPassIn.Nickname = nickname;
                        Token toReturn = Register(nameToPassIn, out status);
                        string toReturnString = JsonConvert.SerializeObject(toReturn);
                        string finalResponse = ResponseBuilder(toReturnString, toReturnString.Length, status);
                        ((SS)payload).BeginSend(finalResponse, null, null);
                    }
                }
                else if (line.Contains("games"))
                {

                }
            }
            else if (line.StartsWith("PUT"))
            {

            }
            else
            {
                // BAD REQUEST
            }
        }



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
