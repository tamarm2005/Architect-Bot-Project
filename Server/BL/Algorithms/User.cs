using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public class UserBL
    {

        public static List<User> Users { get; set; } = new List<User>
        {
             new User(1, "JohnDoe", "password123", "", "", ""),
             new User(2, "JaneSmith", "securePass!", "", "", ""),
        };
    }

}
