using FreeSql.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace FreeSql.Cloud.Model
{
    public class UnitInvokedInfo
    {
        [Column(Name = "id", IsPrimary = true, StringLength = 128)]
        public string Id { get; set; }

        [Column(Name = "create_time", ServerTime = DateTimeKind.Utc, CanUpdate = false)]
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        [Column(Name = "remark", StringLength = 50)]
        public string Remark { get; set; } = "FreeSql.Cloud TCC/SAGA";
    }
}
