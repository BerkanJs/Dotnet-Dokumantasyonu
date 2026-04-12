// ─────────────────────────────────────────────────────────────────────────────
// Global using direktifleri — tüm test dosyalarında otomatik olarak geçerlidir.
// Her test dosyasına ayrı ayrı "using Xunit;" yazmaktan kurtarır.
// ─────────────────────────────────────────────────────────────────────────────

global using Xunit;
global using FluentAssertions;
global using Moq;

// Proje sınıfları
global using KitabeviMVC.Data;
global using KitabeviMVC.Models.Entities;
global using KitabeviMVC.Models.ViewModels;
global using KitabeviMVC.Repositories;
global using KitabeviMVC.Services;
global using KitabeviMVC.Features.Kitaplar;

// EF Core
global using Microsoft.EntityFrameworkCore;

// Genel
global using System.Net;
global using System.Net.Http.Json;
