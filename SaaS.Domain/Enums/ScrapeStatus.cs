using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Enums
{
    public enum ScrapeStatus
    {
        RUNNING,
        PENDING,
        COMPLETED,
        FAILED,
        CANCELLED
    }
}
