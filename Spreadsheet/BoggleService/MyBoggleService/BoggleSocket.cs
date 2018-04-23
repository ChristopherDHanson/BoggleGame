// Authors Bryce Hansen, Christopher Hanson for CS 3500, April 23, 2018

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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyBoggleService
{
    class BoggleSocket
    {
        //Socket and server objects
        private SSListener server;
        private SS stringSocket;
        private BoggleService BService;

        //Class variables for determining current request
        private static readonly Regex contentLengthPattern = new Regex(@"^content-length: (\d+)", RegexOptions.IgnoreCase);
        private int contentLength = 0;
        private string firstLine;
        
        /// <summary>
        /// Starts the server; sets up connection on specifies SS; starts receiving, using ReadLines as callback
        /// </summary>
        /// <param name="ss"></param>
        public BoggleSocket(SS ss)
        {
            BService = new BoggleService();
            this.stringSocket = ss;
            stringSocket.BeginReceive(ReadLines, stringSocket);
        }

        /// <summary>
        /// Reads each incoming line and parses out the JSON object along with the first line of the request
        /// </summary>
        /// <param name="str"></param>
        /// <param name="payload"></param>
        private void ReadLines(string str, object payload)
        {
            if (str.Trim().Length == 0 && contentLength > 0)
            {
                ((SS)payload).BeginReceive(HandleRequest, payload, contentLength);
            }
            else if (str.Trim().Length == 0)
            {
                HandleRequest(null, payload);
            }
            else if (firstLine != null)
            {
                Match m = contentLengthPattern.Match(str);
                if (m.Success)
                {
                    contentLength = int.Parse(m.Groups[1].ToString());
                }
                ((SS)payload).BeginReceive(ReadLines, payload);
            }
            else
            {
                firstLine = str;
                ((SS) payload).BeginReceive(ReadLines, payload);
            }
        }

        /// <summary>
        /// Handles each request given a string object in JSON format and an active string socket.
        /// </summary>
        /// <param name="requestStr"></param>
        /// <param name="payload"></param>
        private void HandleRequest (string requestStr, object payload)
        {
            HttpStatusCode status;
            string finalResponse;

            if (firstLine.StartsWith("GET"))
            {
                String[] splitLine = firstLine.Split('/','?');

                string isBrief = "";
                
                if (splitLine.Length > 2)
                {
                    String[] splitBrief = splitLine[4].Split('=',' ');
                    isBrief = splitBrief[1];
                }

                // Gets status of specified game; either brief or not
                GameStatus returnStatus = GetStatus(splitLine[3], isBrief, out status);
                String returnStatusString = JsonConvert.SerializeObject(returnStatus);
                returnStatusString = ResponseBuilder(returnStatusString, returnStatusString.Length, status);
                Reset(returnStatusString, payload);
            }
            else if (firstLine.StartsWith("POST"))
            {
                string[] splitLine = firstLine.Split('/');
                if (splitLine[2].StartsWith("users"))
                {
                    // Registers new user by calling BoggleServer.Register via BoggleSocket.Register
                    UserName nameToPassIn = JsonConvert.DeserializeObject<UserName>(requestStr);
                    Token toReturn = Register(nameToPassIn, out status);
                    string toReturnString = JsonConvert.SerializeObject(toReturn);
                    finalResponse = ResponseBuilder(toReturnString, toReturnString.Length, status);
                    Reset(finalResponse, payload);
                }
                else if (splitLine[2].StartsWith("games"))
                {
                    // Joins game by callind BoggleServer.Join via BoggleSocker.Join
                    TokenTime tempTkTm = JsonConvert.DeserializeObject<TokenTime>(requestStr);
                    GameIDOnly toReturn = Join(tempTkTm, out status);
                    string toReturnString = JsonConvert.SerializeObject(toReturn);
                    finalResponse = ResponseBuilder(toReturnString, toReturnString.Length, status);
                    Reset(finalResponse, payload);
                }
            }
            else if (firstLine.StartsWith("PUT"))
            {
                string[] splitLine = firstLine.Split('/');

                if (splitLine[2].Equals("games"))
                {
                    string gameID = splitLine[3].Split(' ')[0];

                    // Plays word in specified game by calling BogglerServer.PlayWord via BoggleSocket.PlayWord
                    TokenWord recievedObject = JsonConvert.DeserializeObject<TokenWord>(requestStr);
                    ScoreOnly  returnScore = PlayWord(recievedObject, gameID, out status);
                    String returnStatusString = JsonConvert.SerializeObject(returnScore);
                    finalResponse = ResponseBuilder(returnStatusString, returnStatusString.Length, status);
                    Reset(finalResponse, payload);
                }
                else
                {
                    // Attempts to cancel join request by calling BoggleServer.CancelJoin via BoggleSocket.CancelJoin
                    Token recievedObject = JsonConvert.DeserializeObject<Token>(requestStr);
                    CancelJoin(recievedObject, out status);
                    String returnStatusString = JsonConvert.SerializeObject(recievedObject);
                    finalResponse = ResponseBuilder(returnStatusString, returnStatusString.Length, status);
                    Reset(finalResponse, payload);
                }
            }
            else
            {
                // Bad request; assemble response, send it back
                finalResponse = ResponseBuilder(null, 0, HttpStatusCode.BadRequest);
                ((SS)payload).BeginSend(finalResponse, (x, y) => { ((SS)payload).Shutdown(SocketShutdown.Both); }, null);
                firstLine = null;
                contentLength = 0;
            }
        }

        /// <summary>
        /// Helper method to assemble responses.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="length"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private string ResponseBuilder(string json, int length, HttpStatusCode status)
        {
            StringBuilder response = new StringBuilder();
            response.AppendLine("HTTP/1.1 "+(int)status+" "+status);
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

        /// <summary>
        /// Helper method to complete sends and reset needed variables.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="payload"></param>
        private void Reset(String s, Object payload)
        {
            ((SS)payload).BeginSend(s, (x, y) => { ((SS)payload).Shutdown(SocketShutdown.Both); }, null);
            firstLine = null;
            contentLength = 0;
        }
    }
}
