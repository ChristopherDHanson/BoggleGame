using CustomNetworking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MyBoggleService
{
    class BoggleSocket
    {
        private SSListener server;
        private BoggleService BService;

        private SS clients;

        private readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();
        private string fullRequest = "";
        private int contentLength = 0;
        private StringReader reader;

        public BoggleSocket(int port)
        {
            BService = new BoggleService();

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
            clients = s;

            // We ask the server to listen for another connection request.  As before, this
            // will happen on another thread.
            server.BeginAcceptSS(ConnectionRequested, null);

            // We create a new ClientConnection, which will take care of communicating with
            // the remote client.  We add the new client to the list of clients, taking 
            // care to use a write lock.
            try
            {
                sync.EnterWriteLock();
                s.BeginReceive(ReadLines, s);
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        private void ReadLines(string str, object payload)
        {
            if (str.Trim().Length > 0 && str.Contains("Content-Length:")) // Content length line
            {
                string[] splitLine = str.Split(':');
                Int32.TryParse(splitLine[1].Trim(), out contentLength);
                ((SS)payload).BeginReceive(ReadLines, payload);
            }
            else if (str.StartsWith("GET") || str.StartsWith("PUT") || str.StartsWith("POST")) // First line
            {
                fullRequest += str;
                ((SS)payload).BeginReceive(ReadLines, payload);
            }
            else if (str.Trim().Length == 0 && contentLength > 0) // Time to read JSON
            {
                ((SS)payload).BeginReceive(ReadLines, payload, contentLength);
            }
            else // Done
            {
                HandleRequest(fullRequest, payload);
            }
        }

        private void HandleRequest (string requestStr, object payload)
        {
            HttpStatusCode status;
            string finalResponse;
            // Parse thru the requestStr, get relevant data
            reader = new StringReader(requestStr);
            string line = reader.ReadLine();
            int contentLength;

            if (line.StartsWith("GET"))
            {
                String[] splitLine = line.Split('/','?');

                string isBrief = "";
                
                if (splitLine.Length > 2)
                    isBrief = splitLine[2];

                GameStatus returnStatus = GetStatus(splitLine[1], isBrief, out status);

                String returnStatusString = JsonConvert.SerializeObject(returnStatus);
                
                returnStatusString = ResponseBuilder(returnStatusString, returnStatusString.Length, status);

                ((SS) payload).BeginSend(returnStatusString,null,null);
            }
            else if (line.StartsWith("POST"))
            {
                string[] splitLine = line.Split('/');
                if (splitLine[2].Equals("users"))
                {
                    while (!line.StartsWith("Content-Length:"))
                        reader.ReadLine();
                    splitLine = line.Split(':');
                    if (Int32.TryParse(splitLine[1].Trim(), out contentLength))
                    {
                        char[] jsonObj = new char[contentLength];
                        while (!line.Contains("{"))
                            reader.Read();
                        reader.Read(jsonObj, 0, contentLength);
                        UserName nameToPassIn = JsonConvert.DeserializeObject<UserName>(jsonObj.ToString());
                        Token toReturn = Register(nameToPassIn, out status);
                        string toReturnString = JsonConvert.SerializeObject(toReturn);
                        finalResponse = ResponseBuilder(toReturnString, toReturnString.Length, status);
                        ((SS)payload).BeginSend(finalResponse, null, null);
                    }
                }
                else if (splitLine[2].Equals("games"))
                {
                    while (!line.StartsWith("Content-Length:"))
                        reader.ReadLine();
                    splitLine = line.Split(':');
                    if (Int32.TryParse(splitLine[1].Trim(), out contentLength))
                    {
                        char[] jsonObj = new char[contentLength];
                        while (!line.Contains("{"))
                            reader.Read();
                        reader.Read(jsonObj, 0, contentLength);
                        TokenTime tempTkTm = JsonConvert.DeserializeObject<TokenTime>(jsonObj.ToString());
                        GameIDOnly toReturn = Join(tempTkTm, out status);
                        string toReturnString = JsonConvert.SerializeObject(toReturn);
                        finalResponse = ResponseBuilder(toReturnString, toReturnString.Length, status);
                        ((SS)payload).BeginSend(finalResponse, null, null);
                    }
                }
            }
            else if (line.StartsWith("PUT"))
            {
                string[] splitLine = line.Split('/');

                if (splitLine[2].Equals("games"))
                {
                    string gameID = splitLine[3];

                    while (!line.StartsWith("Content-Length:"))
                        reader.ReadLine();

                    splitLine = line.Split(':');

                    if (Int32.TryParse(splitLine[1], out contentLength))
                    {
                        string jSonOb = "";
                        char[] temp = new char[contentLength];
                        while (!reader.Read().Equals("{"))
                        {
                            reader.Read(temp, 0, contentLength);
                            jSonOb = temp.ToString();
                        }

                        TokenWord recievedObject = JsonConvert.DeserializeObject<TokenWord>(jSonOb);
                        PlayWord(recievedObject, gameID, out status);
                        String returnStatusString = JsonConvert.SerializeObject(recievedObject);
                        finalResponse = ResponseBuilder(returnStatusString, returnStatusString.Length, status);
                        ((SS) payload).BeginSend(finalResponse, null, null);
                    }
                }
                else
                {
                    while (!line.StartsWith("Content-Length:"))
                        reader.ReadLine();

                    splitLine = line.Split(':');

                    if (Int32.TryParse(splitLine[1], out contentLength))
                    {
                        string jSonOb = "";
                        char[] temp = new char[contentLength];
                        while (!reader.Read().Equals("{"))
                        {
                            reader.Read(temp, 0, contentLength);
                            jSonOb = temp.ToString();
                        }

                        Token recievedObject = JsonConvert.DeserializeObject<Token>(jSonOb);
                        CancelJoin(recievedObject, out status);
                        String returnStatusString = JsonConvert.SerializeObject(recievedObject);
                        finalResponse = ResponseBuilder(returnStatusString, returnStatusString.Length, status);
                        ((SS)payload).BeginSend(finalResponse, null, null);
                    }
                }

            }
            else
            {
                finalResponse = ResponseBuilder(null, 0, HttpStatusCode.BadRequest);
                ((SS)payload).BeginSend(finalResponse, null, null);
            }
        }

        private void NextResponse(string requestStr, object payload)
        {
            reader = new StringReader(reader.ReadToEnd() + requestStr);
        }

        private string ResponseBuilder(string json, int length, HttpStatusCode status)
        {
            StringBuilder response = new StringBuilder();
            response.AppendLine("HTTP//1.1 "+(int)status+" "+status);
            response.AppendLine("Content-Length: "+length);
            response.AppendLine("Content-Type: application/json; charset=utf-8");
            response.AppendLine("");
            response.AppendLine(json);

            return response.ToString();
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
