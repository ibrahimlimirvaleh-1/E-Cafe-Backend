using System;
using System.Collections.Generic;

namespace ECafe.Infrastructure.Db.Entities;


public partial class Category
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual Restaurant Restaurant { get; set; } = null!;
}
