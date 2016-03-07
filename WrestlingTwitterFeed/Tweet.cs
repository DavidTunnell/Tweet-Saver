using System;
using System.Collections.Generic;

namespace TweetSaver
{
    public class Tweet
    {
        public int primaryKey { get; set; }
        public string twitterPrimaryKey { get; set; }
        public string name { get; set; }
        public string screenName { get; set; }
        public string profileImageUrl { get; set; }
        public string tweetText { get; set; }
        public DateTime postTime { get; set; }
        public List<string> tweetImageUrls = new List<string>();
        public string rawJson { get; set; }
    }
}