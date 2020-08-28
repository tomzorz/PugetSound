using System.ComponentModel.DataAnnotations;

namespace PugetSound.Data.Models
{
    public class UserScore
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public long Score { get; set; }
    }
}
