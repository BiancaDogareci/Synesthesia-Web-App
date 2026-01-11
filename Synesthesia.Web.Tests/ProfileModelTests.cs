//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Identity;
//using Moq;
//using Synesthesia.Web.Models;
//using Synesthesia.Web.Pages;
//using Xunit;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Http;

//namespace Synesthesia.Web.Tests
//{

//    public class ProfileModelTests
//    {
//        private Mock<UserManager<AppUser>> MockUserManager()
//        {
//            var store = new Mock<IUserStore<AppUser>>();
//            return new Mock<UserManager<AppUser>>(
//                store.Object, null, null, null, null, null, null, null, null
//            );
//        }



//        // simulate a logged-in user by mocking UserManager.GetUserAsync to return a fake AppUser
//        // it confirms that your page displays the correct user information
//        [Fact]
//        public async Task OnGet_WhenUserExists_SetsProfileProperties()
//        {
//            // Arrange
//            var mockUserManager = MockUserManager();
//            var testUser = new AppUser
//            {
//                UserName = "testuser",
//                ProfilePicture = "/img/test.png",
//                Bio = "Test bio"
//            };

//            mockUserManager
//                .Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
//                .ReturnsAsync(testUser);

//            var pageModel = new ProfileModel(mockUserManager.Object);

//            // Act
//            await pageModel.OnGet();

//            // Assert
//            Assert.Equal("testuser", pageModel.Username);
//            Assert.Equal("/img/test.png", pageModel.ProfilePicture);
//            Assert.Equal("Test bio", pageModel.Bio);
//        }



//        // simulates the scenario where no user is logged in or UserManager returns null
//        // ensures your page handles “not logged in” or “user unexpectedly missing” gracefully
//        [Fact]
//        public async Task OnGet_WhenNoUser_ProfilePropertiesAreNull()
//        {
//            var mockUserManager = MockUserManager();

//            mockUserManager
//                .Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
//                .ReturnsAsync((AppUser?)null);

//            var pageModel = new ProfileModel(mockUserManager.Object);

//            await pageModel.OnGet();

//            Assert.Null(pageModel.Username);
//            Assert.Null(pageModel.ProfilePicture);
//            Assert.Null(pageModel.Bio);
//        }
//    }

//}
