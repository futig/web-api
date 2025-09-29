﻿using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var entity = userRepository.FindById(userId);
        if (entity is null) return NotFound();

        var dto = mapper.Map<UserDto>(entity);
        return Ok(dto);
    }

    [HttpHead("{userId}")]
    public IActionResult HeadUser([FromRoute] Guid userId)
    {
        var exists = userRepository.FindById(userId) is not null;
        Response.Body = Stream.Null;
        return exists ? Ok(exists) : NotFound();
    }

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult CreateUser([FromBody] CreateUserDto user)
    {
        if (user is null) return BadRequest();

        if (string.IsNullOrEmpty(user.Login))
        {
            ModelState.AddModelError("login", "Error");
            return UnprocessableEntity(ModelState);
        }

        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var entityToCreate = mapper.Map<UserEntity>(user);
        var created = userRepository.Insert(entityToCreate);

        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = created.Id },
            created.Id);
    }

    [HttpPut("{userId}")]
    [Consumes("application/json")]
    public IActionResult UpdateUser([FromBody] UpdateUserDto user, [FromRoute] string userId)
    {
        if (user is null) return BadRequest();

        var validation = ValidateUserDto(user);
        if (validation is not null) return validation;

        if (!Guid.TryParse(userId, out var id)) return BadRequest();

        var entity = userRepository.FindById(id);
        if (entity is null)
            return CreateUser(mapper.Map<CreateUserDto>(user));

        mapper.Map(user, entity);

        userRepository.Update(entity);
        return NoContent();
    }

    [HttpPatch("{userId}")]
    [Consumes("application/json-patch+json")]
    public IActionResult PartiallyUpdateUser([FromBody] JsonPatchDocument<UpdateUserDto> patchDoc, [FromRoute] string userId)
    {
        if (patchDoc is null) return BadRequest();

        if (!Guid.TryParse(userId, out var id)) return NotFound();

        var entity = userRepository.FindById(id);
        if (entity is null) return NotFound();

        var dtoToPatch = mapper.Map<UpdateUserDto>(entity);

        var preValidation = ValidatePatchDocument(patchDoc);
        if (preValidation is not null) return preValidation;

        patchDoc.ApplyTo(dtoToPatch, ModelState);
        TryValidateModel(dtoToPatch);

        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        mapper.Map(dtoToPatch, entity);
        userRepository.Update(entity);

        return NoContent();
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] string userId)
    {
        if (!Guid.TryParse(userId, out var id)) return NotFound();

        if (userRepository.FindById(id) is null) return NotFound();

        userRepository.Delete(id);
        return NoContent();
    }

    [HttpGet]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 20);

        var pageList = userRepository.GetPage(pageNumber, pageSize);
        var users = mapper.Map<IEnumerable<UserDto>>(pageList);

        var previousPageLink = pageList.HasPrevious
            ? linkGenerator.GetUriByAction(HttpContext, nameof(GetUsers), values: new { pageNumber = pageNumber - 1, pageSize })
            : null;
        var nextPageLink = pageList.HasNext
            ? linkGenerator.GetUriByAction(HttpContext, nameof(GetUsers), values: new { pageNumber = pageNumber + 1, pageSize })
            : null;

        var paginationHeader = new
        {
            previousPageLink,
            nextPageLink,
            totalCount = pageList.TotalCount,
            pageSize,
            currentPage = pageNumber,
            totalPages = (int)Math.Ceiling((double)pageList.TotalCount / pageSize)
        };

        Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        return Ok(users);
    }

    [HttpOptions]
    public IActionResult Options()
    {
        Response.Headers.Add("Allow", "GET, POST, OPTIONS");

        return Ok();
    }

    private IActionResult? ValidateUserDto(UpdateUserDto userDto)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        if (string.IsNullOrWhiteSpace(userDto.FirstName))
        {
            ModelState.AddModelError("firstName", "First name cannot be empty.");
            return UnprocessableEntity(ModelState);
        }

        if (string.IsNullOrWhiteSpace(userDto.LastName))
        {
            ModelState.AddModelError("lastName", "Last name cannot be empty.");
            return UnprocessableEntity(ModelState);
        }

        return null;
    }

    private IActionResult? ValidatePatchDocument(JsonPatchDocument<UpdateUserDto> patchDoc)
    {
        foreach (var op in patchDoc.Operations)
        {
            var path = op.path?.Trim('/');
            if (string.Equals(path, "login", StringComparison.OrdinalIgnoreCase))
            {
                var value = op.value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    ModelState.AddModelError("login", "Login cannot be empty.");
                    return UnprocessableEntity(ModelState);
                }
                if (!IsAlphaNum(value))
                {
                    ModelState.AddModelError("login", "Login must not contain special characters.");
                    return UnprocessableEntity(ModelState);
                }
            }
            else if (string.Equals(path, "firstName", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(op.value?.ToString()))
                {
                    ModelState.AddModelError("firstName", "First name cannot be empty.");
                    return UnprocessableEntity(ModelState);
                }
            }
            else if (string.Equals(path, "lastName", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(op.value?.ToString()))
                {
                    ModelState.AddModelError("lastName", "Last name cannot be empty.");
                    return UnprocessableEntity(ModelState);
                }
            }
        }

        return null;
    }

    private static bool IsAlphaNum(string s)
    {
        foreach (var ch in s)
            if (!char.IsLetterOrDigit(ch)) return false;
        return true;
    }
}