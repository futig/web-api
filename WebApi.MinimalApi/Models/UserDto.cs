using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WebApi.MinimalApi.Models;

public class UserDto
{
    public Guid Id { get; set; }
    public string Login { get; set; }
    public string FullName { get; set; }
    public int GamesPlayed { get; set; }
    public Guid? CurrentGameId { get; set; }
}

public class CreateUserDto
{
    [Required]
    [RegularExpression("^[0-9\\p{L}]*$",
        ErrorMessage = "Login should contain only letters or digits")]
    public string Login { get; set; }

    [DefaultValue("Biba")]
    public string FirstName { get; set; }

    [DefaultValue("Abobov")]
    public string LastName { get; set; }
}

public class UpdateUserDto
{
    [Required]
    [RegularExpression("^[0-9\\p{L}]*$",
        ErrorMessage = "Login should contain only letters or digits")]
    public string Login { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}