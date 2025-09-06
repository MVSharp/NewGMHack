using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewGMHack.CommunicationModel.Models;

namespace NewGmHack.GUI.Abstracts
{
    public interface IRoomManager
    {
        void UpdateRoomList(IEnumerable<Roommate> roommates);
    }
}
