﻿using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Boggle
{
    [ServiceContract]
    public interface IBoggleService
    {
        /// <summary>
        /// Sends back index.html as the response body.
        /// </summary>
        [WebGet(UriTemplate = "/api")]
        Stream API();

        /// <summary>
        /// Registers a new user.
        /// If either user.Name or user.Email is null or is empty after trimming, responds with status code Forbidden.
        /// Otherwise, creates a user, returns the user's token, and responds with status code Created. 
        /// </summary>
        [WebInvoke(Method = "POST", UriTemplate = "/users")]
        Token Register(UserName user);

        /// <summary>
        /// Joins game. 
        /// </summary>
        [WebInvoke(Method = "POST", UriTemplate = "/games")]
        GameIDOnly Join(TokenTime user);

        /// <summary>
        /// Cancels join
        /// </summary>
        [WebInvoke(Method = "PUT", UriTemplate = "/games")]
        void CancelJoin(Token user);

        /// <summary>
        /// Plays a word
        /// </summary>
        [WebInvoke(Method = "PUT", UriTemplate = "/games/{GameID}")]
        ScoreOnly PlayWord(TokenWord word, string GameID);

        /// <summary>
        /// Gets game status update
        /// </summary>
        [WebGet(UriTemplate = "/games/{GameID}?brief={isBrief}")]
        GameStatus GetStatus(string GameID, string isBrief);
    }
}
