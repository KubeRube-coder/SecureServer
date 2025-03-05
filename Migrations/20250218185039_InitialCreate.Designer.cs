﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecureServer.Data;

#nullable disable

namespace SecureServer.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250218185039_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("SecureServer.Models.ActiveToken", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateTime>("ExpiryDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("JwtToken")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("ActiveTokens");
                });

            modelBuilder.Entity("SecureServer.Models.Blacklist", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("DiscordId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Login")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("SteamId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Blacklist");
                });

            modelBuilder.Entity("SecureServer.Models.Mod", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("NameDWS")
                        .HasColumnType("longtext");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.Property<string>("image_url")
                        .HasColumnType("longtext");

                    b.Property<string>("modsby")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("price")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Mods");
                });

            modelBuilder.Entity("SecureServer.Models.moddevelopers", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("mods")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("modsby")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("nameOfMod")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("moddevelopers");
                });

            modelBuilder.Entity("SecureServer.Models.PendingMod", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("NameDWS")
                        .HasColumnType("longtext");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.Property<string>("image_url")
                        .HasColumnType("longtext");

                    b.Property<bool>("prem")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("price")
                        .HasColumnType("int");

                    b.Property<string>("refused")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("pendingMods");
                });

            modelBuilder.Entity("SecureServer.Models.premmods", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("mods")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("modsby")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("premPrice")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("premmods");
                });

            modelBuilder.Entity("SecureServer.Models.purchasesInfo", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateTime>("date")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime>("expires_date")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("modId")
                        .HasColumnType("int");

                    b.Property<int>("whoBuyed")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("purchasesInfos");
                });

            modelBuilder.Entity("SecureServer.Models.Servers", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ip")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("mods")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("owner_id")
                        .HasColumnType("int");

                    b.HasKey("id");

                    b.ToTable("Servers");
                });

            modelBuilder.Entity("SecureServer.Models.subscription", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateTime>("expireData")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("login")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("steamid")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<bool>("subActive")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("Id");

                    b.ToTable("subscription");
                });

            modelBuilder.Entity("SecureServer.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<bool>("Banned")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("ClaimedMods")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("DiscordId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("JwtSecretKey")
                        .HasColumnType("longtext");

                    b.Property<string>("Login")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("SteamId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<float>("balance")
                        .HasColumnType("float");

                    b.Property<string>("lastip")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("role")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}
