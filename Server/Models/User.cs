using Models;

namespace Models
{
    public class User
    {
        private int v;

        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<Apartment> Apartments { get; set; } = new List<Apartment>();
        public string DesignPreferences { get; set; } = string.Empty;


        public User()
        {

        }

        public User(int id, string userName, string email, string password, string designPreferences, string v)
        {
            this.Id = id;
            this.Username = userName;
            this.Email = email;
            this.Password = password;
            this.DesignPreferences = designPreferences;
        }

        public User(int v)
        {
            this.v = v;
        }
    }
}
