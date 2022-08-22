using System;

namespace net60_webapi
{
    // db1 实体类
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Point { get; set; }
    }

    // db2 实体类
    public class Goods
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Stock { get; set; }
    }

    // db3 实体类
    public class Order
    {
        public Guid Id { get; set; }
        public OrderStatus Status { get; set; }
        public enum OrderStatus { Pending, Success, Canceled }
        public DateTime CreateTime { get; set; }
    }
}
