using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

public sealed class StatusChangeConflictException(string message) : Exception(message);
