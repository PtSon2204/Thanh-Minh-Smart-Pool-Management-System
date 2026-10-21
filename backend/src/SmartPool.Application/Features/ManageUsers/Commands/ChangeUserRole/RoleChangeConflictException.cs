using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;

public sealed class RoleChangeConflictException(string message) : Exception(message);
