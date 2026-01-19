using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.Chatbot;

public interface IChatService
{
    Task<string> GetResponseAsync(List<ChatContent> chatHistoryContent);
}
