# v2.6.332
### Client
* Update: Optimizing UDP Processing
* Update: Improving Grabage Colelctor
* Update: Async Disposal
* Update: Windows: Upgrade WinDiver to 2.2.2
* Update: Improve performance

### Server
* Feature: Allow disabling LogAnonymizer in server config
* Feature: NetScanner protector
* Feature: Access ServerConfig overwrite
* Update: Optimizing UDP Processing
* Update: Reporting improved; prevent too many duplicate errors
* Update: Windows updater write its log
* Update: Improve performance
* Update: Add NetScan to Track log
* Update: Imporve the Tracer Log File format
* Fix: File Access Server throw access voliatile randomly
* Fix: Disconnecting Idle users after an hour of inactivity of FileAccess Server
* Fix: Linux Auto Installation
* Fix: Too many session recovery after hot restart

# v2.6.329
### Server
* Fix: Report CPU Usage on Linux
* Fix: Windows Server Auto Update
* Fix: Windows Auto Install
* Fix: Stop accepting connection on specific errors
* Update: Report more config on start up

# v2.6.327
### Server
* Fix: Error on centos
* Feature: Report CPU usage to access server
* Feature: Add TcpConnectWait control
* Feature: Add TcpChannelCount control

# v2.6.326
### Client
* Feature: Windows: Compile as Win-x64. NET runtime is not required anymore.
* Feature: Windows: WebView2 is optional. Run UI in the default web browser if WebView2 was not installed
* Fix: Unable to connect to IpV6 supported site on chrome when server IpV6 is not configured
* Fix: Hold some TCP connections
* Fix: The client tries to connect to the IPv6 endpoint regardless of its connectivity
* Fix: Show Blank screen
* Update: Restore auto-reconnect
* Update: Improve performance and memory usage
* Update: Windows x86 (32-bit) is not supported anymore

### Server
* Feature: Report IPv6 support to client
* Feature: Add -domain to File AccessServer to set access-key endpoint will set to certificate domain
* Fix: Update Script doesn't work
* Fix: Hold some TCP connections
* Fix: Delay in showing command-line helps for File Access Server
* Fix: "Sequence contains no elements" Error when could not find any Public IP
* Update: Improve performance and memory usage
* Update: Improve Logging
* Update: Change config JSON property name for SessionOptions and TrackingOptions

# v2.5.323
### Client
* Update: Improve messages of disconnection reason
* Feature: Replace Always ON with auto-reconnect
* Fix: Anonymize VpnHood Server IP in diagnose  
* Fix: Windows Installer

### Server
* Update: Improve Log for AccessServer API CALL
* Update: Port Tracker
* Update: Improve session recovery
* Fix: Critical bug that consume much resources

# v2.4.321
### Server
* Update: Remove extra trace log from OS

# v2.4.320
### Client
* Update: Upgrade to .NET 7

### Server
* Feature: Compile as a self-contained; No need for .Net Framework Runtime
* Update: Upgrade to .NET 7
* Update: New Installation For Linux 
* Update: New Installation For Windows Server
* Update: New Installation For Docker
* Update: Improve logging
* Update: Removing App Launcher project
* Fix: Error on Windows Server. unsupported option or level was specified in a getsockopt or setsockopt call
* Fix: Archiving the log file when another instance of the server is already running
* Fix: Preventing running multiple instances from once location

# v2.4.318
### Client
* Feature: Show a message a device disconnected by your device
* Feature: Android TV support
* Update: Updating IP Location Database
* Update: Improve Client Battery Usage
* Update: Show SupportId (sid) to servers list
* Update: Remove Legacy AccessKey support
* Fix: Randomly select previous profile in UI

# v2.4.310
### Client
* Update: Removing Google Ads

# v2.4.307
### Client
* Feature: Add basic advertising support. Ouch!
* Update: Upgrade to android 12.1

# v2.4.304
### Client
* Fix: Triming AccessKey
* Update: Improve detecting countries

### Server
* Fix: Nlog doesn't log some events
* Fix: Docker Installation on ubuntu
* Update: Add destination port in tracking

# v2.4.303
### Client
* Update: Simplify Client's Country exclusion

### Server
* Update: Improve Session Management

### Developer
* Update: Move VpnHood.Client.WebUI to a standalone repo

# v2.4.299
### Client
* Fix: Windows: Installation Package

# v2.4.297
### Server
* Fix: Reporting Negative usage

# v2.4.296
### Client
* Fix: Windows: WebView2 could not be installed on some devices

### Server
* Feature: Add linux docker package
* Update: Sync all active sessions to access the server every few minutes
* Fix: Maintenance mode detection
* Fix: Synching sessions to access server on shut down

# v2.4.295
### Client
* Update: Tune TCP connections for games
* Fix: Error when setting PacketCapture include filter

### Server
* Feature: Server sends its last config error to access server
* Fix: TcpHost is already Started error
* Fix: Linux installation on some distribution
* Fix: LogLevel.Trace in DiagnoseMode

# v2.4.292
### Client
* Update: Improve stability and memory usage

### Server
* Update: Use keep-alive for TCP timeout
* Fix: Double Configure at startup
* Fix: Sending multiple requests to access server for session recovery
* Fix: Memory leak! Some dead sessions remain in memory
* Fix: Memory leak! TcpProxy remains in memory when just one peer has gone
* Fix: Memory leak! UdpProxy remains in memory
* Fix: Unusual Thread creation
* Fix: UDP Packet loss

# v2.3.291
### Client
* Fix: Android: Improve performance and stability in Android
* Fix: Add time-stamp to logger

### Server
* Update: Move Sessions options to AccessServer via ServerConfig
* Fix: Catch a lost packet when removing TcpDatagramChannel

# v2.3.290
### Client
* Fix: Crash on Android 12

### Server
* Feature: LocalPort and ClientIP Tracking Options
* Update: Set default port for -ep command
* Update: Use NLog.config in app binary folder if it does not exists in working folder

# v2.3.289
### Client
* Update: Add Logging Policy Warning
* Update: Create Private Server Link

### Server
* Update: Linux: Some issue in installation
* Fix: Maintenance mode detection

# v2.3.287
### Client
* Update: Upgrade to .NET 6
* Update: Diagnose just check some HTTPS sites to check internet connectivity
* Update: Windows: Disable right click on App WebView
* Fix: Not a valid calendar for the given culture

### Server
* Update: Upgrade to .NET 6
* Update: Configuration by access server
* Feature: Close session faster by handling client bye request
* Fix: Refact IP addresses in the log 

# v2.2.283
### Client
* Feature: Allow to have multi-endpoints in AccessToken
* Feature: Create IPv6 tunnel when a client has access to a server by IPv6
* Feature: Add "Exclude Local Network" to UI settings
* Fix: UDP Channel

### Server
* Feature: Dynamic configuration from AccessServer
* Feature: Multi listeners for different EndPoints
* Fix: Few bug in disposing
* Fix: linux: systemctl restart VpnHoodServer 

# v2.1.276
* Feature: IPv6 Support
* Fix: Some packet loss in ping 

# v2.1.276
* Feature: IPv6 Support
* Fix: Some packet loss in ping 

# v2.0.272
* Feature: Block all IPv6 Global Unicast to prevent leak 
* Fix: Android: Vpn Connection keeps open after disconnecting
* Fix: Android: Crash in android 5.1
* Fix: IpFilter miss some IPs of countries
* Update: Improve the speed of establishing the connection

# v2.0.271
### Client
* Feature: Server Redirection
* Feature: Server Maintenance mode detection
* Feature: Validate packets integrity in UdpChannel
* Update: Android: Hide notification icon on the lock screen
* Update: Improve Performance and Memory usage
* Change: Stop supporting the old version
* Fix: Instability in reconnecting and disconnecting
* Fix: IpFilter didn't work properly when more than one country was selected
* Fix: Android: System Notification remain connected after disconnect
* Fix: Android: Some Apps were not shown in the AppFilter list (Require Permission: QUERY_ALL_PACKAGES)
* Fix: Android: Crash if a selected app in AppFilter does not exist anymore
* Fix: Android: Crash after disconnect

### Server
* Feature: Host Restart with REST access server (No UDP yet)
* Feature: Validate packets integrity in UdpChannel
* Update: Stop supporting the old version
* Update: Improve Performance and Memory usage
* Update: New REST AccessServer protocol
* Change: Stop supporting the old version

### Developer
* Update: Respect C# Nullable Reference Types
* Update: Mass Code cleanup
* Update: Decouple access manager from server to access server

# v1.3.254
### Client
* Feature: Android: Add Manage button to the system notification
* Fix: Casual packet loss!
* Fix: Empty error message after immediate disconnection
* Fix: Could not open the Protocol page
* Fix: Android: No window open by pressing menu items
* Fix: Windows: Could not load WinDivert

### Server
* Fix: Casual packet loss!

# v1.3.253
### Client
* Feature: IpFilter by countries
* Feature: Android: Exclude local networks from VPN
* Feature: Android: Add disconnect to device notification bar
* Update: Improve Performance and Memory usage
* Update: Reduce number of Public Server hints
* Fix: Windows: Didn't bypass Some local network traffics

### Server
* Update: Imporve Performance and Memory usage

# v1.2.250
### Client
* Update: Display error for unsupported client
* Fix: Random Crash!
* Fix: No error message when Client lost the connection

### Server
* Update: Check session id for each UdpPacket
* Update: Reject unSupported client
* Fix: Updater on linux
* Fix: Nlog maxArchiveDays maxArchiveFiles

# v1.2.249
* Feature: Reset apps TCP connections immediately after VPN get connected
* Update: Significantly optimize performance & stability
* Update: Improve power usage

### Client
* Fix: Attempting to connect after stopping the VPN

# v1.2.248
### Client
* Feature: Windows 7 Support
* Feature: Add "What's New" link in the main menu
* Fix: Windows: Display Main window location depending on TaskBar position
* Fix: Freeze network after auto reconnect
* Fix: Freeze network when UDP connection lost
* Fix: Freeze network after network lost
* Fix: Selecting current active server causes disconnection

### Developer
* Fix: Public Server in Android Sample

# v1.2.247
* Feature: Add UDP Protocol
* Update: Improve datagram performance
* Update: Improve overall performance
* Update: Improve messaging security
* Update: Improve Stability
* Fix: Problem in sending some UDP packets
* Fix: Json length is too big

### Developer
* Upgrade to SharpPcap 6.0

# v1.1.242
### Client
* Update: Windows: Installer check for new updates before installation

# v1.1.241
### Client
* Fix: Freeze in Disconnecting state
* Fix: Reconnection

# v1.1.240
### Client
* Fix: Diangnostic report "No Internet", when there is internet 
* Update: Windows: Change Updater

# v1.1.238
### Client
* Feature: Set allowed or disallowed Apps that can use VPN
* Update: Windows & Linux: Check TargetFramework before update
* Update: Show warning for Public Server

# v1.1.236
### Client
* Fix: Android: Crash when sending feedback on Android 11
* Fix: Connection already in progress error when changing server
* Update: Show traffic speed

### Server
* Update: Auto restart if VpnHoodServer stops unexpectedly
* Fix: Typo error in default.pfx filename for FileAccessServer
* Fix: linux: Stop working after server update

# v1.1.235
### Client
* New: New public server
* New: Windows: Bypass local network from tunneling

# v1.1.232
### Client
* New: Android: Prevent landscape orientation
* Update: Significantly improve speed and stability
* Update: Automatically remove profiles when token does not exist
* Update: add some log EventId
* Fix: UDP loss in mass UDP traffic

### Server
* New: Send ClientVersion to AccessServer
* Update: drop Hello version 1 support
* Update: Significantly improve speed and stability
* Update: Automatically remove profiles when token does not exist
* Update: add some log EventId
* Fix: token is ignored when created by FileAccessServer
* Fix: UDP loss in mass UDP traffic

# v1.1.217
### Server
* New: Rest server validate Self-Signed certificates by RestCertificateThumbprint property in appsettings

# v1.1.216
* New: Updater has completely changed

### Server
* New: Add stop command to stop all server instance
* New: Linux: Add installation script
* New: Linux: Run server as a service
* Change: rename "run" command to "start"

# v1.1.202
### Client
* New: Change server list page
* New: Android: Change system status bar color to match UI
* New: Windows: Change icon on notification area by connection status
* Fix: Big UI on some devices
* Update: Change Public Server Name

### Server
* Update: Start new log file on every run

# v1.1.197
### Client
* Fix: rejecting accesskey with vh://

### Server
* New: Report Linux Distribution info
* New: Report connected ClientVersion
* Fix: "Permission Denied" error in Linux while sending some UDP packets

# v1.1.195
### Client
* Feature: Modern UI
* Feature: Show usage if there is any limitation
* Feature: Windows: reconnect last connection after auto update
* Fix: Windows: Fix main window size
* Fix: Windows: launch application after installation

### Server
* Fix: Use last command line argument after auto update

# v1.1.187
### Client
* Feature: Windows: Use new standalone UI
* Feature: Windows: Add Context menu to system tray
* Update: Add Microsoft WebView2 Edge to Windows Installer prerequisites
* Update: Send ClientVersion to server
* Fix: accesskey prefix

### Server
* Fix: Reading server port number from appsettings.json
* Update: Support multiple public IP and Amazon ElasticIP

# v1.1.184
### Client
* Feature: Auto Configure Windows Defender Firewall
* Update: Improve diagnosing
* Fix: Significantly Improve connection stability & speed
* Fix: Displaying connection state

### Server
* Fix: Unhandled NullReferenceException on ping packets
* Fix: Improve server memory cleanup
* Fix: Prevent new conenction after session disposed
* Fix: Speed Monitor and connection idle state
* Fix: Improve connection stability and lost packets
* Feature: ICMP logging for client and server with IsDiagnoseMode
* Feature: Use NLog for logging
* Feature: Auto initialize NLog config and appsettings.json

# v1.1.177
* Fix: Client close the entire VPN connection when a requested site refuse a connection

# v1.1.176
* Feature: Client can detect its expired session

### Client
* Change: Always Open the main window at start if App is already running

### Developer
* Change: Update TcpDatagramHeader from binary to TcpDatagramChannelRequest json
* Change: Move IDevice and IPacketCapture to VpnHood.Client.Device module
* Developer: Add Simple Sample for Windows Client usage
* Developer: Fix PublishApps.ps1 scripts to create publish folder when it does not exist

# v1.1.138
* Fix: Checking update from the Internet

### Server
* Update: add subdomain when creating self-signed certifiates with random CN

# v1.1.91
* Fix: AppUpdater throw error if UpdateUrl in publish.json was empty string

### Client
* Update: Add client prefix to Bug Report File Name
* Update: Close Bug Report bottom page after sending report
* Update: Separate SPA from VpnHood.Client.App.UI. Make it easier for developers to use custom SPA
* Update: Change Anonymous IP masking from *.*.x.x to "*.x.x.*"
* Update: Diagnose set Last error to "Diagnose has been finished" if there is not other error
* Fix: Dark Icon
* Fix: Open BugReport page on external web browser
* Fix: Disable Diagnose button when a connection already diagnosing
* Fix: Reporting .NET version instead of App Version

# v1.1.75
* Initial Release
