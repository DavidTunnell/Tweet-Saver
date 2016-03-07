using System;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using LinqToTwitter;
using System.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace TweetSaver
{
    public partial class index : Page
    {
        public static List<Tweet> tweetList = new List<Tweet>();

        public void Page_Load(object sender, EventArgs e)
        {
            createTweetObjectList();
            generateTweetUi();
        }

        protected void createTweetObjectList()
        {
            //get authenitcation to use twitter api
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };
            var twitterCtx = new TwitterContext(auth);
            //get last X posts by selected twitter username including retweets
            var statusTweets =
            (from tweet in twitterCtx.Status
             where tweet.Type == StatusType.User &&
                   tweet.ScreenName == ConfigurationManager.AppSettings["twitterScreenName"] &&
                   tweet.IncludeEntities == true &&
                   tweet.IncludeRetweets == true &&
                   tweet.Count == Int32.Parse(ConfigurationManager.AppSettings["tweetCount"])
             select tweet)
            .ToList();
            var jsonData = twitterCtx.RawResult;
            //deserialize json data
            dynamic deserializedJson = JsonConvert.DeserializeObject(jsonData);
            //iterate through each post and get relevant data to display tweets - add to list tweet objects
            foreach (var postArray in deserializedJson)
            {
                Tweet currentTweet = new Tweet();
                currentTweet.twitterPrimaryKey = postArray.id_str;
                currentTweet.name = postArray.user.name;
                currentTweet.screenName = postArray.user.screen_name;
                currentTweet.profileImageUrl = postArray.user.profile_image_url;
                currentTweet.tweetText = postArray.text;
                string createdAt = postArray.created_at;
                currentTweet.postTime = DateTime.ParseExact(createdAt, "ddd MMM dd HH:mm:ss %K yyyy", CultureInfo.InvariantCulture.DateTimeFormat);
                //a tweet can have multiple images
                if (postArray.entities.media != null)
                {
                    foreach (var mediaEntity in postArray.entities.media)
                    {
                        string currentUrlJson = JsonConvert.SerializeObject(mediaEntity.media_url);
                        currentTweet.tweetImageUrls.Add(currentUrlJson.Replace("\"", ""));
                    }
                }
                string currentTweetsJson = JsonConvert.SerializeObject(postArray);
                currentTweet.rawJson = currentTweetsJson;
                tweetList.Add(currentTweet);
            }
        }

        protected void generateTweetUi()
        {
            int count = 0;
            foreach (var item in tweetList)
            {
                Panel tweetPanel = new Panel();
                tweetPanel.BorderColor = System.Drawing.Color.Black;
                tweetPanel.BorderWidth = 1;
                tweetPanel.Attributes["style"] = "padding: 20px; margin 20px; width: 20%;";
                tweets.Controls.Add(tweetPanel);
                tweetPanel.Controls.Add(new LiteralControl("<div style=\"float: right;\">"));
                CheckBox tweetChecker = new CheckBox();
                tweetChecker.AutoPostBack = true;
                tweetChecker.ID = "checkBox" + count;
                tweetChecker.Checked = tweetExistsInDatabase(ConfigurationManager.AppSettings["connectionString"], item.twitterPrimaryKey);
                tweetChecker.CheckedChanged += new EventHandler(checkChanged);
                tweetPanel.Controls.Add(tweetChecker);
                tweetPanel.Controls.Add(new LiteralControl(item.postTime.ToShortTimeString() + " " + item.postTime.ToShortDateString() + "</div>"));
                tweetPanel.Controls.Add(new LiteralControl("<img src=\"" + item.profileImageUrl + "\">"));
                tweetPanel.Controls.Add(new LiteralControl("@" + item.screenName));
                tweetPanel.Controls.Add(new LiteralControl("<p>" + item.tweetText + "</p>"));
                foreach (var img in item.tweetImageUrls)
                {
                    tweetPanel.Controls.Add(new LiteralControl("<span><img style=\"width:35%; height:35%;\" src=\"" + img + "\"></span>"));
                }
                count++;
            }
        }

        protected void checkChanged(object sender, EventArgs e)
        {
            var checkBoxSent = (CheckBox)sender;
            bool currentlyChecked = checkBoxSent.Checked;
            int listLocation = Int32.Parse(checkBoxSent.ClientID.Split('x')[1]);
            if (currentlyChecked)
            {
                addToDatabase(ConfigurationManager.AppSettings["connectionString"], listLocation);
            }
            else
            {
                removeFromDatabase(ConfigurationManager.AppSettings["connectionString"], tweetList[listLocation].twitterPrimaryKey);
            }
        }

        protected void addToDatabase(string connectionString, int listLocation)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO SelectedTweets (twitterPrimaryKey, name, screenName, profileImageUrl, tweetText, postTime, tweetImageUrls1, tweetImageUrls2, tweetImageUrls3, tweetImageUrls4, rawJson) VALUES (@twitterPrimaryKey, @name, @screenName, @profileImageUrl, @tweetText, @postTime, @tweetImageUrls1, @tweetImageUrls2, @tweetImageUrls3, @tweetImageUrls4, @rawJson);";
                command.Parameters.AddWithValue("@twitterPrimaryKey", tweetList[listLocation].twitterPrimaryKey);
                command.Parameters.AddWithValue("@name", tweetList[listLocation].name);
                command.Parameters.AddWithValue("@screenName", tweetList[listLocation].screenName);
                command.Parameters.AddWithValue("@profileImageUrl", tweetList[listLocation].profileImageUrl);
                command.Parameters.AddWithValue("@tweetText", tweetList[listLocation].tweetText);
                command.Parameters.AddWithValue("@postTime", tweetList[listLocation].postTime);
                if (tweetList[listLocation].tweetImageUrls.Count > 0)
                {
                    command.Parameters.AddWithValue("@tweetImageUrls1", tweetList[listLocation].tweetImageUrls[0]);
                }
                else
                {
                    command.Parameters.AddWithValue("@tweetImageUrls1", DBNull.Value);
                }
                if (tweetList[listLocation].tweetImageUrls.Count > 1)
                {
                    command.Parameters.AddWithValue("@tweetImageUrls2", tweetList[listLocation].tweetImageUrls[1]);
                }
                else
                {
                    command.Parameters.AddWithValue("@tweetImageUrls2", DBNull.Value);
                }
                if (tweetList[listLocation].tweetImageUrls.Count > 2)
                {
                    command.Parameters.AddWithValue("@tweetImageUrls3", tweetList[listLocation].tweetImageUrls[2]);
                }
                else
                {
                    command.Parameters.AddWithValue("@tweetImageUrls3", DBNull.Value);
                }
                if (tweetList[listLocation].tweetImageUrls.Count > 3)
                {
                    command.Parameters.AddWithValue("@tweetImageUrls4", tweetList[listLocation].tweetImageUrls[3]);
                }
                else
                {
                    command.Parameters.AddWithValue("@tweetImageUrls4", DBNull.Value);
                }
                command.Parameters.AddWithValue("@rawJson", tweetList[listLocation].rawJson);
                connection.Open();
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        protected void removeFromDatabase(string connectionString, string twitterPrimaryKey)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM SelectedTweets WHERE twitterPrimaryKey = @twitterPrimaryKey;";
                command.Parameters.AddWithValue("@twitterPrimaryKey", twitterPrimaryKey);
                connection.Open();
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        protected bool tweetExistsInDatabase(string connectionString, string twitterPrimaryKey)
        {
            int exists = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CASE WHEN EXISTS (SELECT * FROM [SelectedTweets] WHERE twitterPrimaryKey = @twitterPrimaryKey) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END";
                command.Parameters.AddWithValue("@twitterPrimaryKey", twitterPrimaryKey);
                connection.Open();
                exists = Convert.ToInt32(command.ExecuteScalar());
                connection.Close();
            }
            //1 - yes, 0 - no
            if (exists == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

//DATABASE TABLE SCRIPT
//USE[SelectedTweets]
//GO

///****** Object:  Table [dbo].[SelectedTweets]    Script Date: 3/1/2016 3:07:53 PM ******/
//SET ANSI_NULLS ON
//GO

//SET QUOTED_IDENTIFIER ON
//GO

//CREATE TABLE[dbo].[SelectedTweets](
//	[id]
//[int] IDENTITY(1,1) NOT NULL,
//    [twitterPrimaryKey] [nvarchar](20) NOT NULL,
//    [name] [nvarchar](50) NOT NULL,
//    [screenName] [nvarchar](50) NOT NULL,
//    [profileImageUrl] [nvarchar](max) NOT NULL,
//    [tweetText] [nvarchar](200) NOT NULL,
//    [postTime] [datetime]
//NOT NULL,
//    [tweetImageUrls1] [nvarchar](max) NULL,
//	[tweetImageUrls2]
//[nvarchar](max) NULL,
//	[tweetImageUrls3]
//[nvarchar](max) NULL,
//	[tweetImageUrls4]
//[nvarchar](max) NULL,
//	[rawJson]
//[nvarchar](max) NOT NULL,
//CONSTRAINT[PK_SelectedTweets] PRIMARY KEY CLUSTERED
//(
//[id] ASC
//)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]
//) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]

//GO

//====================================================================================================================//

//SAMPLE JSON OF INDIVIDUAL TWEET
//{{
//  "created_at": "Tue Feb 23 16:50:22 +0000 2016",
//  "id": 702173668181409792,
//  "id_str": "702173668181409792",
//  "text": "RT @TNADixie: TONIGHT on @IMPACTWRESTLING it's #Lockdown. Can't watch it live? Set your DVR! It's worth it. https://t.co/Jg0k6Uq0lk",
//  "source": "<a href=\"http://twitter.com/download/iphone\" rel=\"nofollow\">Twitter for iPhone</a>",
//  "truncated": false,
//  "in_reply_to_status_id": null,
//  "in_reply_to_status_id_str": null,
//  "in_reply_to_user_id": null,
//  "in_reply_to_user_id_str": null,
//  "in_reply_to_screen_name": null,
//  "user": {
//    "id": 35338217,
//    "id_str": "35338217",
//    "name": "TNA WRESTLING",
//    "screen_name": "IMPACTWRESTLING",
//    "location": "Nashville, TN",
//    "description": "IMPACT WRESTLING on @PopTV Tuesdays at 9/8c!",
//    "url": "https://t.co/nCEdcBWP0z",
//    "entities": {
//      "url": {
//        "urls": [
//          {
//            "url": "https://t.co/nCEdcBWP0z",
//            "expanded_url": "http://www.IMPACTWRESTLING.com",
//            "display_url": "IMPACTWRESTLING.com",
//            "indices": [
//              0,
//              23
//            ]
//          }
//        ]
//      },
//      "description": {
//        "urls": []
//      }
//    },
//    "protected": false,
//    "followers_count": 417557,
//    "friends_count": 119,
//    "listed_count": 2980,
//    "created_at": "Sat Apr 25 23:16:33 +0000 2009",
//    "favourites_count": 6946,
//    "utc_offset": -21600,
//    "time_zone": "Central Time (US & Canada)",
//    "geo_enabled": true,
//    "verified": true,
//    "statuses_count": 55293,
//    "lang": "en",
//    "contributors_enabled": false,
//    "is_translator": false,
//    "is_translation_enabled": false,
//    "profile_background_color": "1A1B1F",
//    "profile_background_image_url": "http://pbs.twimg.com/profile_background_images/431598777976102912/R4xoQ02b.jpeg",
//    "profile_background_image_url_https": "https://pbs.twimg.com/profile_background_images/431598777976102912/R4xoQ02b.jpeg",
//    "profile_background_tile": false,
//    "profile_image_url": "http://pbs.twimg.com/profile_images/692710057893441537/71n1D_Vh_normal.jpg",
//    "profile_image_url_https": "https://pbs.twimg.com/profile_images/692710057893441537/71n1D_Vh_normal.jpg",
//    "profile_banner_url": "https://pbs.twimg.com/profile_banners/35338217/1455814189",
//    "profile_link_color": "2FC2EF",
//    "profile_sidebar_border_color": "FFFFFF",
//    "profile_sidebar_fill_color": "252429",
//    "profile_text_color": "666666",
//    "profile_use_background_image": true,
//    "has_extended_profile": false,
//    "default_profile": false,
//    "default_profile_image": false,
//    "following": false,
//    "follow_request_sent": false,
//    "notifications": false
//  },
//  "geo": null,
//  "coordinates": null,
//  "place": null,
//  "contributors": null,
//  "retweeted_status": {
//    "created_at": "Tue Feb 23 16:22:31 +0000 2016",
//    "id": 702166656009707520,
//    "id_str": "702166656009707520",
//    "text": "TONIGHT on @IMPACTWRESTLING it's #Lockdown. Can't watch it live? Set your DVR! It's worth it. https://t.co/Jg0k6Uq0lk",
//    "source": "<a href=\"http://twitter.com\" rel=\"nofollow\">Twitter Web Client</a>",
//    "truncated": false,
//    "in_reply_to_status_id": null,
//    "in_reply_to_status_id_str": null,
//    "in_reply_to_user_id": null,
//    "in_reply_to_user_id_str": null,
//    "in_reply_to_screen_name": null,
//    "user": {
//      "id": 84368747,
//      "id_str": "84368747",
//      "name": "Dixie Carter",
//      "screen_name": "TNADixie",
//      "location": "Nashville",
//      "description": "President of TNA Wrestling. \n\nFollow me on Instagram @TNADixie and Facebook here: https://t.co/bfyjld6fqM\n\n@IMPACTWrestling every Tuesday on @PopTV at 9/8c.",
//      "url": "https://t.co/nkfRJNsSGi",
//      "entities": {
//        "url": {
//          "urls": [
//            {
//              "url": "https://t.co/nkfRJNsSGi",
//              "expanded_url": "http://www.impactwrestling.com",
//              "display_url": "impactwrestling.com",
//              "indices": [
//                0,
//                23
//              ]
//            }
//          ]
//        },
//        "description": {
//          "urls": [
//            {
//              "url": "https://t.co/bfyjld6fqM",
//              "expanded_url": "http://on.fb.me/23rY97L",
//              "display_url": "on.fb.me/23rY97L",
//              "indices": [
//                82,
//                105
//              ]
//            }
//          ]
//        }
//      },
//      "protected": false,
//      "followers_count": 351404,
//      "friends_count": 98,
//      "listed_count": 3221,
//      "created_at": "Thu Oct 22 16:51:50 +0000 2009",
//      "favourites_count": 556,
//      "utc_offset": -21600,
//      "time_zone": "Central Time (US & Canada)",
//      "geo_enabled": false,
//      "verified": true,
//      "statuses_count": 10924,
//      "lang": "en",
//      "contributors_enabled": false,
//      "is_translator": false,
//      "is_translation_enabled": false,
//      "profile_background_color": "C0DEED",
//      "profile_background_image_url": "http://pbs.twimg.com/profile_background_images/360131928/dixietwitter4.jpg",
//      "profile_background_image_url_https": "https://pbs.twimg.com/profile_background_images/360131928/dixietwitter4.jpg",
//      "profile_background_tile": false,
//      "profile_image_url": "http://pbs.twimg.com/profile_images/684378570332979201/iQy6dleQ_normal.png",
//      "profile_image_url_https": "https://pbs.twimg.com/profile_images/684378570332979201/iQy6dleQ_normal.png",
//      "profile_banner_url": "https://pbs.twimg.com/profile_banners/84368747/1448036986",
//      "profile_link_color": "0084B4",
//      "profile_sidebar_border_color": "C0DEED",
//      "profile_sidebar_fill_color": "DDEEF6",
//      "profile_text_color": "333333",
//      "profile_use_background_image": true,
//      "has_extended_profile": false,
//      "default_profile": false,
//      "default_profile_image": false,
//      "following": false,
//      "follow_request_sent": false,
//      "notifications": false
//    },
//    "geo": null,
//    "coordinates": null,
//    "place": null,
//    "contributors": null,
//    "is_quote_status": false,
//    "retweet_count": 29,
//    "favorite_count": 37,
//    "entities": {
//      "hashtags": [
//        {
//          "text": "Lockdown",
//          "indices": [
//            33,
//            42
//          ]
//        }
//      ],
//      "symbols": [],
//      "user_mentions": [
//        {
//          "screen_name": "IMPACTWRESTLING",
//          "name": "TNA WRESTLING",
//          "id": 35338217,
//          "id_str": "35338217",
//          "indices": [
//            11,
//            27
//          ]
//        }
//      ],
//      "urls": [],
//      "media": [
//        {
//          "id": 702166413641801728,
//          "id_str": "702166413641801728",
//          "indices": [
//            94,
//            117
//          ],
//          "media_url": "http://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//          "media_url_https": "https://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//          "url": "https://t.co/Jg0k6Uq0lk",
//          "display_url": "pic.twitter.com/Jg0k6Uq0lk",
//          "expanded_url": "http://twitter.com/TNADixie/status/702166656009707520/photo/1",
//          "type": "photo",
//          "sizes": {
//            "large": {
//              "w": 720,
//              "h": 404,
//              "resize": "fit"
//            },
//            "thumb": {
//              "w": 150,
//              "h": 150,
//              "resize": "crop"
//            },
//            "small": {
//              "w": 340,
//              "h": 191,
//              "resize": "fit"
//            },
//            "medium": {
//              "w": 600,
//              "h": 337,
//              "resize": "fit"
//            }
//          }
//        }
//      ]
//    },
//    "extended_entities": {
//      "media": [
//        {
//          "id": 702166413641801728,
//          "id_str": "702166413641801728",
//          "indices": [
//            94,
//            117
//          ],
//          "media_url": "http://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//          "media_url_https": "https://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//          "url": "https://t.co/Jg0k6Uq0lk",
//          "display_url": "pic.twitter.com/Jg0k6Uq0lk",
//          "expanded_url": "http://twitter.com/TNADixie/status/702166656009707520/photo/1",
//          "type": "animated_gif",
//          "sizes": {
//            "large": {
//              "w": 720,
//              "h": 404,
//              "resize": "fit"
//            },
//            "thumb": {
//              "w": 150,
//              "h": 150,
//              "resize": "crop"
//            },
//            "small": {
//              "w": 340,
//              "h": 191,
//              "resize": "fit"
//            },
//            "medium": {
//              "w": 600,
//              "h": 337,
//              "resize": "fit"
//            }
//          },
//          "video_info": {
//            "aspect_ratio": [
//              180,
//              101
//            ],
//            "variants": [
//              {
//                "bitrate": 0,
//                "content_type": "video/mp4",
//                "url": "https://pbs.twimg.com/tweet_video/Cb6YogpUYAAIn3R.mp4"
//              }
//            ]
//          }
//        }
//      ]
//    },
//    "favorited": false,
//    "retweeted": false,
//    "possibly_sensitive": false,
//    "lang": "en"
//  },
//  "is_quote_status": false,
//  "retweet_count": 29,
//  "favorite_count": 0,
//  "entities": {
//    "hashtags": [
//      {
//        "text": "Lockdown",
//        "indices": [
//          47,
//          56
//        ]
//      }
//    ],
//    "symbols": [],
//    "user_mentions": [
//      {
//        "screen_name": "TNADixie",
//        "name": "Dixie Carter",
//        "id": 84368747,
//        "id_str": "84368747",
//        "indices": [
//          3,
//          12
//        ]
//      },
//      {
//        "screen_name": "IMPACTWRESTLING",
//        "name": "TNA WRESTLING",
//        "id": 35338217,
//        "id_str": "35338217",
//        "indices": [
//          25,
//          41
//        ]
//      }
//    ],
//    "urls": [],
//    "media": [
//      {
//        "id": 702166413641801728,
//        "id_str": "702166413641801728",
//        "indices": [
//          108,
//          131
//        ],
//        "media_url": "http://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//        "media_url_https": "https://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//        "url": "https://t.co/Jg0k6Uq0lk",
//        "display_url": "pic.twitter.com/Jg0k6Uq0lk",
//        "expanded_url": "http://twitter.com/TNADixie/status/702166656009707520/photo/1",
//        "type": "photo",
//        "sizes": {
//          "large": {
//            "w": 720,
//            "h": 404,
//            "resize": "fit"
//          },
//          "thumb": {
//            "w": 150,
//            "h": 150,
//            "resize": "crop"
//          },
//          "small": {
//            "w": 340,
//            "h": 191,
//            "resize": "fit"
//          },
//          "medium": {
//            "w": 600,
//            "h": 337,
//            "resize": "fit"
//          }
//        },
//        "source_status_id": 702166656009707520,
//        "source_status_id_str": "702166656009707520",
//        "source_user_id": 84368747,
//        "source_user_id_str": "84368747"
//      }
//    ]
//  },
//  "extended_entities": {
//    "media": [
//      {
//        "id": 702166413641801728,
//        "id_str": "702166413641801728",
//        "indices": [
//          108,
//          131
//        ],
//        "media_url": "http://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//        "media_url_https": "https://pbs.twimg.com/tweet_video_thumb/Cb6YogpUYAAIn3R.jpg",
//        "url": "https://t.co/Jg0k6Uq0lk",
//        "display_url": "pic.twitter.com/Jg0k6Uq0lk",
//        "expanded_url": "http://twitter.com/TNADixie/status/702166656009707520/photo/1",
//        "type": "animated_gif",
//        "sizes": {
//          "large": {
//            "w": 720,
//            "h": 404,
//            "resize": "fit"
//          },
//          "thumb": {
//            "w": 150,
//            "h": 150,
//            "resize": "crop"
//          },
//          "small": {
//            "w": 340,
//            "h": 191,
//            "resize": "fit"
//          },
//          "medium": {
//            "w": 600,
//            "h": 337,
//            "resize": "fit"
//          }
//        },
//        "source_status_id": 702166656009707520,
//        "source_status_id_str": "702166656009707520",
//        "source_user_id": 84368747,
//        "source_user_id_str": "84368747",
//        "video_info": {
//          "aspect_ratio": [
//            180,
//            101
//          ],
//          "variants": [
//            {
//              "bitrate": 0,
//              "content_type": "video/mp4",
//              "url": "https://pbs.twimg.com/tweet_video/Cb6YogpUYAAIn3R.mp4"
//            }
//          ]
//        }
//      }
//    ]
//  },
//  "favorited": false,
//  "retweeted": false,
//  "possibly_sensitive": false,
//  "lang": "en"
//}}