﻿using System;
using System.Collections.Generic;
using System.Text;
using YoutubeExplode.Models;

namespace SharedCode.YT
{
    public class VideoDetails
    {
        public string id { get; set; }
        public string ChannelName { get; set; }
        public string Title { get; set; }
        public IEnumerable<string> qualities { get; set; }
        public ThumbnailSet thumbnails { get; set; }
    }
}
