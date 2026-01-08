using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebHooks.Domain;

public enum WebhookDeliveryStatus
{
    Received = 0,
    Published = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Dead = 5
}
