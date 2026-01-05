using Moq;
using Xunit;
using Microsoft.Azure.Cosmos;
using TradingBrain.Models;
using IGModels;
using IGWebApiClient;
using IGWebApiClient.Models;
using dto.endpoint.positions.get.otc.v1;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradingBrain.Tests
{
    public class MainAppTests
    {
        private readonly Mock<Database> _mockDb;
        private readonly Mock<Database> _mockAppDb;
        private readonly Mock<Container> _mockContainer;
        private readonly Mock<IGContainer> _mockIgContainer;
        private readonly Mock<IGContainer> _mockIgContainer2;
        private readonly string _testEpic = "IX.D.NASDAQ.CASH.IP";
        private readonly string _testStrategy = "RSI";
        private readonly string _testResolution = "1MIN";

        public MainAppTests()
        {
            _mockDb = new Mock<Database>();
            _mockAppDb = new Mock<Database>();
            _mockContainer = new Mock<Container>();
            _mockIgContainer = new Mock<IGContainer>();
            _mockIgContainer2 = new Mock<IGContainer>();
        }

        private MainApp CreateMainApp()
        {
            // Based on the actual constructor: MainApp(Database? db, Database? appDb, string epic, 
            //                                  IGContainer? igContainer, IGContainer? igContainer2, 
            //                                  string strategy = "SMA", string resolution = "")
            return new MainApp(
                _mockDb.Object,
                _mockAppDb.Object,
                _testEpic,
                _mockIgContainer.Object,
                _mockIgContainer2.Object,
                _testStrategy,
                _testResolution);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesSuccessfully()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.NotNull(mainApp);
            Assert.Equal(_testEpic, mainApp.epicName);
            Assert.Equal(_testStrategy, mainApp.strategy);
            Assert.Equal(_testResolution, mainApp.resolution);
        }

        [Fact]
        public void Constructor_WithNullDatabase_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainApp(null, _mockAppDb.Object, _testEpic, _mockIgContainer.Object,
                    _mockIgContainer2.Object, _testStrategy, _testResolution));
        }

        [Fact]
        public void Constructor_WithNullAppDatabase_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainApp(_mockDb.Object, null, _testEpic, _mockIgContainer.Object,
                    _mockIgContainer2.Object, _testStrategy, _testResolution));
        }

        [Fact]
        public void Constructor_InitializesTradeErrorDictionary()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.NotNull(mainApp.TradeErrors);
            Assert.NotEmpty(mainApp.TradeErrors);
            Assert.Contains("SUCCESS", mainApp.TradeErrors.Keys);
            Assert.Contains("INSUFFICIENT_FUNDS", mainApp.TradeErrors.Keys);
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void MainApp_Properties_AreAccessible()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.NotNull(mainApp.epicName);
            Assert.NotNull(mainApp.strategy);
            Assert.NotNull(mainApp.resolution);
            Assert.NotNull(mainApp.TradeErrors);
            Assert.NotNull(mainApp.requestedTrades);
            Assert.NotNull(mainApp.the_db);
            Assert.NotNull(mainApp.the_app_db);
        }

        [Fact]
        public void MainApp_CanSetAndGetProperties()
        {
            // Arrange
            var mainApp = CreateMainApp();
            var testTb = new TradingBrainSettings();

            // Act
            mainApp.tb = testTb;
            mainApp.paused = true;
            mainApp.pausedAfterNGL = true;

            // Assert
            Assert.Equal(testTb, mainApp.tb);
            Assert.True(mainApp.paused);
            Assert.True(mainApp.pausedAfterNGL);
        }

        #endregion

        #region Setup Tests

        [Fact]
        public void SetupTradeErrors_ContainsExpectedErrors()
        {
            // Arrange
            var mainApp = CreateMainApp();

            // Assert
            Assert.Contains("SUCCESS", mainApp.TradeErrors.Keys);
            Assert.Contains("INSUFFICIENT_FUNDS", mainApp.TradeErrors.Keys);
            Assert.Contains("MARKET_CLOSED", mainApp.TradeErrors.Keys);
            Assert.Contains("ACCOUNT_NOT_ENABLED_TO_TRADING", mainApp.TradeErrors.Keys);
            Assert.Contains("DUPLICATE_ORDER_ERROR", mainApp.TradeErrors.Keys);
            Assert.Contains("POSITION_NOT_FOUND", mainApp.TradeErrors.Keys);
        }

        #endregion

        #region Basic Instance Tests

        [Fact]
        public void MainApp_Instance_IsNotNull()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.NotNull(mainApp);
        }

        [Fact]
        public void MainApp_EpicName_MatchesConstructorParameter()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.Equal(_testEpic, mainApp.epicName);
        }

        [Fact]
        public void MainApp_Strategy_MatchesConstructorParameter()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.Equal(_testStrategy, mainApp.strategy);
        }

        [Fact]
        public void MainApp_Resolution_MatchesConstructorParameter()
        {
            // Arrange & Act
            var mainApp = CreateMainApp();

            // Assert
            Assert.Equal(_testResolution, mainApp.resolution);
        }

        #endregion

        #region Common Functions Tests

        [Fact]
        public void CommonFunctions_GetEpicList_ReturnsCorrectCount()
        {
            // Arrange
            string[] epics = { "IX.D.NASDAQ.CASH.IP", "IX.D.FTSE.CASH.IP" };

            // Act
            var result = CommonFunctions.GetEpicList(epics);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("IX.D.NASDAQ.CASH.IP", result[0].Epic);
        }

        [Fact]
        public void CommonFunctions_GetKey_ReturnsValidKey()
        {
            // Act
            string key = CommonFunctions.GetKey();

            // Assert
            Assert.NotNull(key);
            Assert.Equal(32, key.Length);
        }

        [Fact]
        public void CommonFunctions_GetIv_ReturnsValidIv()
        {
            // Act
            string iv = CommonFunctions.GetIv();

            // Assert
            Assert.NotNull(iv);
            Assert.Equal(16, iv.Length);
        }

        [Fact]
        public void CommonFunctions_EncryptDecrypt_RoundTrip()
        {
            // Arrange
            string originalText = "TestData";
            string key = CommonFunctions.GetKey();
            string iv = CommonFunctions.GetIv();

            // Act
            string encrypted = CommonFunctions.EncryptText(originalText, key, iv);
            string decrypted = CommonFunctions.DecryptText(encrypted, key, iv);

            // Assert
            Assert.NotNull(encrypted);
            Assert.NotEqual(originalText, encrypted); // Should be encrypted
            Assert.Equal(originalText, decrypted);     // Should decrypt back to original
        }

        [Fact]
        public void CommonFunctions_IsDangerous_DetectsXSS()
        {
            // Arrange
            string safeText = "Hello World";
            string dangerousText = "<script>alert('xss')</script>";

            // Act
            bool isSafeDangerous = CommonFunctions.Is_Dangerous(safeText);
            bool isDangerousDangerous = CommonFunctions.Is_Dangerous(dangerousText);

            // Assert
            Assert.False(isSafeDangerous);
            Assert.True(isDangerousDangerous);
        }

        [Fact]
        public void CommonFunctions_GetRandomString_GeneratesValidLength()
        {
            // Arrange
            int length = 16;

            // Act
            string randomString = CommonFunctions.GetRandomString(length);

            // Assert
            Assert.NotNull(randomString);
            Assert.Equal(length, randomString.Length);
        }

        #endregion
    }
}