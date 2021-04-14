using System.ComponentModel.DataAnnotations;

namespace PugetSound.Data.Models
{
    public class UserData
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string FriendlyName { get; set; }
    }
}
