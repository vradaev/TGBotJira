﻿// <auto-generated />
using System;
using JIRAbot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JIRAbot.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.20")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("JIRAbot.Clients", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Clients");
                });

            modelBuilder.Entity("JIRAbot.Comment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("CommentText")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("MessageId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("RequestId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("RequestId");

                    b.ToTable("Comments");
                });

            modelBuilder.Entity("JIRAbot.Group", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("GroupId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Сlient_id")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("Groups");
                });

            modelBuilder.Entity("JIRAbot.JiraTask", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("JiraKey")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("RequestId")
                        .HasColumnType("integer");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("RequestId");

                    b.ToTable("JiraTasks");
                });

            modelBuilder.Entity("JIRAbot.Request", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("GroupId")
                        .HasColumnType("integer");

                    b.Property<string>("MessageId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("TypeId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.HasIndex("TypeId");

                    b.ToTable("Requests");
                });

            modelBuilder.Entity("JIRAbot.RequestStatusHistory", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("NewStatus")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OldStatus")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("RequestId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("RequestId");

                    b.ToTable("RequestStatusHistories");
                });

            modelBuilder.Entity("JIRAbot.RequestType", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("RequestTypes");
                });

            modelBuilder.Entity("JIRAbot.Comment", b =>
                {
                    b.HasOne("JIRAbot.Request", "Request")
                        .WithMany("Comments")
                        .HasForeignKey("RequestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Request");
                });

            modelBuilder.Entity("JIRAbot.JiraTask", b =>
                {
                    b.HasOne("JIRAbot.Request", "Request")
                        .WithMany("JiraTasks")
                        .HasForeignKey("RequestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Request");
                });

            modelBuilder.Entity("JIRAbot.Request", b =>
                {
                    b.HasOne("JIRAbot.Group", "Group")
                        .WithMany("Requests")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("JIRAbot.RequestType", "Type")
                        .WithMany()
                        .HasForeignKey("TypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Group");

                    b.Navigation("Type");
                });

            modelBuilder.Entity("JIRAbot.RequestStatusHistory", b =>
                {
                    b.HasOne("JIRAbot.Request", "Request")
                        .WithMany("StatusHistories")
                        .HasForeignKey("RequestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Request");
                });

            modelBuilder.Entity("JIRAbot.Group", b =>
                {
                    b.Navigation("Requests");
                });

            modelBuilder.Entity("JIRAbot.Request", b =>
                {
                    b.Navigation("Comments");

                    b.Navigation("JiraTasks");

                    b.Navigation("StatusHistories");
                });
#pragma warning restore 612, 618
        }
    }
}