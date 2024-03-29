﻿using FreeSql.DataAnnotations;
using System;
using System.Data;

namespace FreeSql.Cloud.Tcc
{
    [Index("{tablename}_idx1", "status")]
    public class TccMasterInfo
    {
        [Column(Name = "tid", IsPrimary = true, StringLength = 128)]
        public string Tid { get; set; }

        [Column(Name = "title")]
        public string Title { get; set; }

        [Column(Name = "total")]
        public int Total { get; set; }

        [Column(Name = "create_time", ServerTime = DateTimeKind.Utc, CanUpdate = false)]
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        [Column(Name = "finish_time")]
        public DateTime FinishTime { get; set; }

        [Column(Name = "status", MapType = typeof(string), StringLength = 10)]
        public TccMasterStatus Status { get; set; }

        [Column(Name = "max_retry_count")]
        public int MaxRetryCount { get; set; } = 30;

        [Column(Name = "retry_interval")]
        public int RetryInterval { get; set; } = 60;

        [Column(Name = "retry_count")]
        public int RetryCount { get; set; }

        [Column(Name = "retry_time")]
        public DateTime RetryTime { get; set; }
    }
    public enum TccMasterStatus { Pending, Confirmed, Canceled, ManualOperation }


    public class TccUnitInfo
    {
        [Column(Name = "tid", IsPrimary = true, StringLength = 128)]
        public string Tid { get; set; }

        [Column(Name = "index", IsPrimary = true)]
        public int Index { get; set; }

        [Column(Name = "description")]
        public string Description { get; set; }

        [Column(Name = "stage", MapType = typeof(string), StringLength = 8)]
        public TccUnitStage Stage { get; set; }

        [Column(Name = "type_name")]
        public string TypeName { get; set; }

        [Column(Name = "state", StringLength = - 1)]
        public string State { get; set; }

        [Column(Name = "state_type_name")]
        public string StateTypeName { get; set; }

        [Column(Name = "create_time", ServerTime = DateTimeKind.Utc, CanUpdate = false)]
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        [Column(Name = "db_key", StringLength = 128)]
        public string DbKey { get; set; }
    }
    public enum TccUnitStage { Try, Confirm, Cancel }
}
