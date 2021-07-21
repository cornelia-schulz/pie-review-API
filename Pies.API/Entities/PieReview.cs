﻿using System;

namespace Pies.API.Entities
{
    public class PieReview
    {
        public Guid Id { get; set; }
        public Guid PieId { get; set; }
        public DateTimeOffset DateCreated { get; set; }
        public Guid UserId { get; set; }
        public string Description { get; set; }
        public int Rating { get; set; }
    }
}
