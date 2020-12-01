using FreeSql.DataAnnotations;
using System;
using System.Data;

namespace FreeSql.Cloud
{
    [Table(Name = "tcc_master")]
    public class TccMaster
    {
        [Column(Name = "tid", IsPrimary = true, StringLength = 128)]
        public string Tid { get; set; }

        [Column(Name = "total")]
        public int Total { get; set; }

        [Column(Name = "tasks", StringLength = -1)]
        public string Tasks { get; set; }

        [Column(Name = "create_time", ServerTime = DateTimeKind.Utc, CanUpdate = false)]
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        [Column(Name = "finish_time")]
        public DateTime FinishTime { get; set; }

        [Column(Name = "status", MapType = typeof(string), StringLength = 10)]
        public TccMasterStatus Status { get; set; }

        [Column(Name = "retry_count")]
        public int RetryCount { get; set; }

        [Column(Name = "retry_time")]
        public DateTime RetryTime { get; set; }
    }
    public enum TccMasterStatus { Pending, Confirmed, Canceled }


    [Table(Name = "tcc_task")]
    public class TccTask
    {
        [Column(Name = "tid", IsPrimary = true, StringLength = 128)]
        public string Tid { get; set; }

        [Column(Name = "index", IsPrimary = true)]
        public int Index { get; set; }

        [Column(Name = "description")]
        public string Description { get; set; }

        [Column(Name = "stage", MapType = typeof(string), StringLength = 8)]
        public TccTaskStage Stage { get; set; }

        [Column(Name = "cloud_name")]
        public string CloudName { get; set; }

        [Column(Name = "type_name")]
        public string TypeName { get; set; }

        [Column(Name = "isolation_level", MapType = typeof(string), StringLength = 16)]
        public IsolationLevel? IsolationLevel { get; set; }

        [Column(Name = "state", StringLength = - 1)]
        public string State { get; set; }

        [Column(Name = "state_type_name")]
        public string StateTypeName { get; set; }

        [Column(Name = "create_time", ServerTime = DateTimeKind.Utc, CanUpdate = false)]
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    }
    public enum TccTaskStage { Try, Confirm, Cancel }

    public class TccTaskLite
    {
        public string CloudName { get; set; }
        public string TypeName { get; set; }
    }
}
