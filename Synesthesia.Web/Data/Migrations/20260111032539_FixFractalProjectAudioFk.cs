using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synesthesia.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFractalProjectAudioFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FractalProjects_AudioFiles_AudioFileId",
                table: "FractalProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedVideos_AudioFiles_AudioFileId",
                table: "SavedVideos");

            migrationBuilder.DropIndex(
                name: "IX_SavedVideos_AudioFileId",
                table: "SavedVideos");

            migrationBuilder.DropIndex(
                name: "IX_FractalProjects_AudioFileId",
                table: "FractalProjects");

            migrationBuilder.DropColumn(
                name: "AudioFileId",
                table: "SavedVideos");

            migrationBuilder.DropColumn(
                name: "AudioFileId",
                table: "FractalProjects");

            migrationBuilder.CreateIndex(
                name: "IX_SavedVideos_AudioId",
                table: "SavedVideos",
                column: "AudioId");

            migrationBuilder.CreateIndex(
                name: "IX_FractalProjects_AudioId",
                table: "FractalProjects",
                column: "AudioId");

            migrationBuilder.AddForeignKey(
                name: "FK_FractalProjects_AudioFiles_AudioId",
                table: "FractalProjects",
                column: "AudioId",
                principalTable: "AudioFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedVideos_AudioFiles_AudioId",
                table: "SavedVideos",
                column: "AudioId",
                principalTable: "AudioFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FractalProjects_AudioFiles_AudioId",
                table: "FractalProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedVideos_AudioFiles_AudioId",
                table: "SavedVideos");

            migrationBuilder.DropIndex(
                name: "IX_SavedVideos_AudioId",
                table: "SavedVideos");

            migrationBuilder.DropIndex(
                name: "IX_FractalProjects_AudioId",
                table: "FractalProjects");

            migrationBuilder.AddColumn<Guid>(
                name: "AudioFileId",
                table: "SavedVideos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AudioFileId",
                table: "FractalProjects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedVideos_AudioFileId",
                table: "SavedVideos",
                column: "AudioFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FractalProjects_AudioFileId",
                table: "FractalProjects",
                column: "AudioFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_FractalProjects_AudioFiles_AudioFileId",
                table: "FractalProjects",
                column: "AudioFileId",
                principalTable: "AudioFiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedVideos_AudioFiles_AudioFileId",
                table: "SavedVideos",
                column: "AudioFileId",
                principalTable: "AudioFiles",
                principalColumn: "Id");
        }
    }
}
