using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RequestProject
{
    public class WorkflowRequester
    {
        private readonly string _connectionString;
        private readonly IConnection _rabbitConnection;
        private readonly string _requestQueue = "work-requests";
        private readonly string _replyQueue = "work-replies";
    }
}
