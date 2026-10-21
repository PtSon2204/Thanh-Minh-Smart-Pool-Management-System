using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageRoles.Commands.ChangeRolePermission;

public sealed class RolePermissionConflictException(string message) : Exception(message);
