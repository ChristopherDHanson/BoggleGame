using System.Collections.Generic;
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
        /// Returns the nth word from dictionary.txt.  If there is
        /// no nth word, responds with code 403. This is a demo;
        /// you can delete it.
        /// </summary>
        [WebGet(UriTemplate = "/word?index={n}")]
        string WordAtIndex(int n);

        /// <summary>
        /// Registers a new user.
        /// If either user.Name or user.Email is null or is empty after trimming, responds with status code Forbidden.
        /// Otherwise, creates a user, returns the user's token, and responds with status code Created. 
        /// </summary>
        [WebInvoke(Method = "POST", UriTemplate = "/users")]
        string Register(UserName user);

        /// <summary>
        /// Joins game. 
        /// </summary>
        [WebInvoke(Method = "POST", UriTemplate = "/games")]
        string Join(TokenTime user);

        /// <summary>
        /// Cancels join
        /// </summary>
        [WebInvoke(Method = "PUT", UriTemplate = "/games")]
        void CancelJoin(Token user);

        /// <summary>
        /// Plays a word
        /// </summary>
        [WebInvoke(Method = "PUT", UriTemplate = "/games/{GameID}")]
        void PlayWord(TokenWord word, string GameID);

        /// <summary>
        /// Gets game status update
        /// </summary>
        [WebGet(UriTemplate = "/games?brief={isBrief}&user={userID}")]
        Status GetAllItems(string isBrief, string userID);
    }


}
