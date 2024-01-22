﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NuttyTree.NetDaemon.Infrastructure.Database;

#nullable disable

namespace NuttyTree.NetDaemon.Infrastructure.Database.Migrations
{
    [DbContext(typeof(NuttyDbContext))]
    [Migration("20240121145048_ToDoListItems")]
    partial class ToDoListItems
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.1");

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Calendar")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("EndDateTime")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsAllDay")
                        .HasColumnType("INTEGER");

                    b.Property<double?>("Latitude")
                        .HasColumnType("REAL");

                    b.Property<string>("Location")
                        .HasColumnType("TEXT");

                    b.Property<double?>("Longitude")
                        .HasColumnType("REAL");

                    b.Property<string>("Person")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("StartDateTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Appointments");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.AppointmentReminderEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("AnnouncementTypes")
                        .HasColumnType("TEXT");

                    b.Property<string>("AppointmentId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int?>("ArriveLeadMinutes")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("LastAnnouncement")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("NextAnnouncement")
                        .HasColumnType("TEXT");

                    b.Property<int?>("NextAnnouncementType")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("NextTravelTimeUpdate")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Priority")
                        .HasColumnType("INTEGER");

                    b.Property<double?>("TravelMiles")
                        .HasColumnType("REAL");

                    b.Property<double?>("TravelMinutes")
                        .HasColumnType("REAL");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("AppointmentId");

                    b.ToTable("AppointmentReminders");
                });

            modelBuilder.Entity("NuttyTree.NetDaemon.Infrastructure.Database.Entities.ToDoListItemEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("ExpiresAt")
                        .HasColumnType("TEXT");

                    b.Property<int>("MinutesEarned")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<Guid>("Uid")
                        .HasColumnType("TEXT");

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
