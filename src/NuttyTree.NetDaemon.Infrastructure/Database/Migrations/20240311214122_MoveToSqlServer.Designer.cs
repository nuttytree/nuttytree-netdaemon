﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NuttyTree.NetDaemon.Infrastructure.Database;

#nullable disable

namespace NuttyTree.NetDaemon.Infrastructure.Database.Migrations
{
    [DbContext(typeof(NuttyDbContext))]
    [Migration("20240311214122_MoveToSqlServer")]
    partial class MoveToSqlServer
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Calendar")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<DateTime?>("EndDateTime")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsAllDay")
                        .HasColumnType("bit");

                    b.Property<double?>("Latitude")
                        .HasColumnType("float");

                    b.Property<string>("Location")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<double?>("Longitude")
                        .HasColumnType("float");

                    b.Property<string>("Person")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<DateTime>("StartDateTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.ToTable("Appointments");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentReminderEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("AnnouncementTypes")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("AppointmentId")
                        .IsRequired()
                        .HasColumnType("nvarchar(100)");

                    b.Property<int?>("ArriveLeadMinutes")
                        .HasColumnType("int");

                    b.Property<DateTime?>("LastAnnouncement")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("NextAnnouncement")
                        .HasColumnType("datetime2");

                    b.Property<int?>("NextAnnouncementType")
                        .HasColumnType("int");

                    b.Property<DateTime?>("NextTravelTimeUpdate")
                        .HasColumnType("datetime2");

                    b.Property<bool>("Priority")
                        .HasColumnType("bit");

                    b.Property<double?>("TravelMiles")
                        .HasColumnType("float");

                    b.Property<double?>("TravelMinutes")
                        .HasColumnType("float");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("AppointmentId");

                    b.ToTable("AppointmentReminders");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.ToDoListItemEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("ExpiresAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("MinutesEarned")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<Guid?>("ReviewUid")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("Uid")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasAlternateKey("Uid");

                    b.ToTable("ToDoListItems");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentReminderEntity", b =>
                {
                    b.HasOne("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentEntity", "Appointment")
                        .WithMany("Reminders")
                        .HasForeignKey("AppointmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Appointment");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentEntity", b =>
                {
                    b.Navigation("Reminders");
                });
#pragma warning restore 612, 618
        }
    }
}