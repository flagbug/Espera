# v2.12.7

## Bugfixes
- Fixed a crash when sending the server discovery message for the mobile client.

# v2.12.6

## Bugfixes
- Fixed a crash when opening the context menu in the list of YouTube videos.

# v2.12.5

## Features
- The installer and Espera itself is now signed.

# v2.12.4

## Bugfixes
- Fixed some updating issues with the portable version.

# v2.12.3

## Bugfixes
- Fixed various installer and updater issues.

# v2.12.2

## Bugfixes
- Fixed some issues with the application setup.
- Fixed the "Update & Restart" notification not disappearing if the changelog is disabled.

# v2.12.1

## Bugfixes
- Added a missing file to the setup package.

# v2.12.0

## Features
- Added an auto-updater for the portable version.
- Added an Update & Restart button when a new version is available.

## Bugfixes
- Fixed a bug that caused the playback to stop when pressing the spacebar in the tag editor.

# v2.11.0

## Changes
- This version is an intermediate release, and migrates Espera to the new installer and updater.

# v2.10.4

## Bugfixes
- Fixed an issue with the artwork loading.
- Fixed a bug that caused increasing memory consumption when searching the local music library.
- Fixed a bug that caused read-only music files not being added to the library.

# v2.10.3

## Bugfixes
- Fixed a bug that allowed the adding of songs to the playlist even when Espera was locked down in the party mode.

# v2.10.2

## Bugfixes
- Fixed an bug that caused some files in the AppData folder to never be removed.
- Changes to the artwork of the song file on the disk are now updated in the library.

# v2.10.1

## Bugfixes
- Fixed the remote control server not starting after being shut down and started again.
- Fixed a memory leak when songs are removed from the library.

# v2.10.0

## Features
- Added a basic MP3 tag editor.
- Added an update notification for the portable version.

## Bugfixes
- Fixed some issues with the Internet connection checks.
- Fixed some problems with the artwork loading.

# v2.9.4

## Improvements
- Improved the SoundCloud and YouTube search time for Espera and the remote control in some cases.
- The network refresh button now indicates its progress.

## Bugfixes
- Fixed the remote control discovery sometimes not working.
- Fixed a bug that caused some album artworks to not display properly.

# v2.9.3

## Bugfixes
- Fixed a crash when sending a bug report with an email address attached.

# v2.9.2

## Bugfixes
- Fixed the remote control not shutting down properly when disabling it.
- Fixed an unnecessary cache startup.

# v2.9.1

## Bugfixes
- Fixed a bug that caused the changelog to be empty after an update.

# v2.9.0

## Features
- Added the Ctrl-F shortcut to focus the search box.

## Improvements
- Raised the YouTube request limit to 50 items.
- Added an explanation for the remote control in the settings menu.

## Changes
- The initial library update is now performed after 5 minutes instead of
  immediately after startup, to improve the startup time and responsiveness.

# v2.8.0

## Features
- Added mobile support for opening and closing the video player.
- Espera now tries to get a SoundCloud stream with higher quality, if available.
- Added an option to open the debug log in the "about" section.

## Bugfixes
- Fixed a crash when closing the application.
- Fixed a crash when changing the audio player.

# v2.7.0

## Features
- Added mobile support for SoundCloud and YouTube.

## Bugfixes
- Fixed a sorting bug for artists that begin with "An".
- Fixed a crash when disconnecting a mobile client.

# v2.6.0

## Features
- The Android remote control is now available in the Google Play Store.
- The library now saves as soon as the update is finished.

## Bugfixes
- Fixed a problem with the automatic artwork download.

# v2.5.1

## Improvements
- Improved the application startup time.
- Improved the network availability discovery.

## Bugfixes
- Fix the song corruption state not resetting if the song can be played.

# v2.5.0

## Features
- Added SoundCloud support.

## Changes
- YouTube now adds the song to the end of the playlist instead of playing it directly.

## Bugfixes
- Fixed issues with the YouTube playback.

# v2.4.1

## Changes
- Renamed the "library" settings to "my music".
- Changed the default playback engine to Windows Media Player.
- Changed the default "my music" and "youtube" column widths.

## Bugfixes
- Fixed a crash that occurred when setting the volume before playing a song.

# v2.4.0

## Features
- Added a new playback engine and added an option menu in the settings to change it.

## Changes
- The maximum library auto update interval can now be 12 hours.
- Changed the default update interval to 3 hours.

## Bugfixes
- Fixed the YouTube link not doing anything when clicked.
- Fixed an application crash when downloading a song from YouTube and the song contained invalid characters for a path.
- Fixed the download progress showing too many decimal places when downloading a song from YouTube.

# v2.3.2

## Bugfixes
- Fixed a bug that caused Espera to crash when adding songs to the playlist.
- Fixed various Drag & Drop issues.

# v2.3.1

## Bugfixes
- Fixed a bug that caused a song to be inserted at the wrong place when dragging it into the playlist.
- Fixed a bug that caused the current selected playlist item to be moved when dragging a song into the playlist.

# v2.3.0

## Features
- Drag & Drop support for moving songs inside the playlist.
- Drag & Drop support for adding local and YouTube songs into the playlist.
- Drag & Drop support for adding YouTube links from the Browser into the playlist.
- Added playback controls in the task bar item.

# v2.2.1

## Bugfixes
- Fixed a bug that caused an incorrect current song marker.
- Fixed some issues with YouTube.
- Fixed the remaining songs indicator spelling "songs" when only one song is left.

# v2.2.0

## Features
- Added a setting to select the default playback behavior when double clicking a song.
  There is an option between "Add To Playlist" and "Play Now".
  
## Changes
- The library isn't purged anymore when the song source path is unavailable.
  
## Bugfixes
- Fixed the network availability not updating when the PC wakes up from sleep mode.
- Fixed the window being resizable by dragging it down on the titlebar, even if it was locked in party mode.
- Fixed a bug that caused the playlist timeout not being ignored when disabled.

# v2.1.0

## Features
- Espera now automatically downloads missing artworks.

## Bugfixes
- Fixed a bug that caused Espera to crash when it encountered a corrupt artwork.
- Fixed a bug that caused some artworks failing to load.
- Fixed a crash when changing the song source path.
- Fixed the playback stopping when pressing the space bar inside the search box.
- Fixed the width of playlist entries.

# v2.0.1

## Changes
- The "local" tab is now called "my music".

## Bugfixes
- Fixed the width of the search box being to small.

# v2.0.0

## Features
- Espera has been completely rewritten.
- Double clicking a song now starts the playback instantly, 
  there's no need to create a new playlist beforehand.
- New songs in the library folder and existing ones are automatically updates 
  if their metadata has changed.
- The YouTube player can now show the video and has support for multiple 
  quality settings.
- Added support for AAC playback.
- Added a completely new update system that applies updates in the background.
- Album covers are now displayed next to the artist.
- Added a slider to change the scaling of the application. This is useful when 
  Espera is displayed on a TV screen.
- Espera can now be remote-controlled via an Android app. The app will soon be 
  released.
- Added instant search for YouTube videos.
- Added support for downloading YouTube videos directly.
- Added more accent colors.
- Added a light application theme.
- Added media key bindings, for playing, pausing as well as playing the next and previous song. 
  They also work even if Espera isn't focused.
- Espera now automatically updates in the background.

## Changes
- Youtube streaming is now enabled by default.
- Espera now requires .NET 4.5 to be installed.

## Bugfixes
- Fixed various YouTube playback issues.
- Fixed some possibilities of library corruption.
- Fixed an issue where pressing the space bar multiple times wouldn't pause or continue a song anymore.

# v1.7.6

## Changes
- YouTube streaming is now enabled by default.

## Bugfixes
- Fixed the settings not being fully accessible in a small window.

# v1.7.5

## Bugfixes
- Fixed a bug that caused a corrupted song not being reported to the user.

# v1.7.4

## Bugfixes
- Fixed a bug that caused the playlist height to reset after an application 
  restart.

# v1.7.3

## Bugfixes
- Fixed some visual bugs.
- Fixed a bug that caused the application to crash when the application is 
  closed.
- Fixed a bug that caused the application to crash when a song is played.

# v1.7.2

## Changes
- Songs from a local network are not cached anymore.

## Bugfixes
- Fixed a bug that caused the memory consumption to increase over time.
- Fixed a bug that caused the application to force focus.
- Fixed a bug that caused the playlist area to take up all the remaining 
  space after a restart.

# v1.7.1

## Bugfixes
- Fixed a bug that caused the application to jump over songs or mark them as 
  corrupt incorrectly.

# v1.7.0

## Features
- Completely new settings menu.
- New music folder selection dialog for Windows Vista and higher.

# v1.6.10

## Bugfixes
- Fixed a bug that caused a timeout when caching YouTube videos.
- Fixed a bug that caused the application to crash during audio playback.

# v1.6.9

## Features
- Added a link to the release notes in the about section.

## Changes
- Updated the website link in the about section.

# v1.6.8

## Bugfixes
- Fixed a bug that caused the application to crash when closing, for libraries 
  that contained songs with special characters in their tags.

# v1.6.7

## Bugfixes
- Fixed YouTube playback.
- Fixed a bug that caused a crash report to not send the version number.

# v1.6.6

## Bugfixes
- Fixed a bug that caused the application to crash when removing a removable 
  device that contained songs from the library.
- Fixed a bug that caused a corruption of the library when removing a 
  removable device that contained songs on a playlist.

# v1.6.5

## Bugfixes
- Fixed a bug that caused the application to crash when removing songs from 
  the playlist.

# v1.6.4

## Bugfixes
- Fixed a bug that caused songs not to be shown in the library when added.

# v1.6.3

## Features
- New color: orange.

## Improvements
- A bug and crash report now also sends the application version.

# v1.6.2

## Bugfixes
- Fixed a bug that caused the application to crash when performing operations 
  that stop a local song.

# v1.6.1

## Bugfixes
- Fixed a problem with the updater.

# v1.6.0

## Features
- A crash of the application now opens a window to report the crash.
- The bug reporting doesn't require a signup at GitHub anymore, but opens a 
  window to report a bug with one click.

## Improvements
- Improved the overall playback performance of local songs.
- Improved the local search performance.
- Smoothened the caching progress of YouTube videos.

# v1.5.0

## Features
- There is now an option to lock the window when entering the party mode.

## Improvements
- Some UI style and visual improvements.

## Bugfixes
- Fixed a bug that caused some settings to be resetted at application 
  startup.
- Fixed a bug that could cause a corruption of the library under certain 
  conditions.

# v1.4.3

## Bugfixes
- Fixed a bug that caused the "Play" command of a playlists context menu to 
  be executable when it shouldn't. When executed regardless, it caused the 
  application to crash.
- Fixed some visual bugs where buttons would be in an enabled state when 
  they shouldn't be.

# v1.4.2

## Bugfixes
- Fixed a bug that caused songs to be only removed from the current playlist 
  instead of all playlists, when removing the songs from the library.
  This bug also caused a corruption of the library and therefore a crash of 
  the application.

# v1.4.1

## Features
- Added a donation link in the settings panel.

## Bugfixes
- Fixed a bug that caused the application to crash when no library existed 
  yet.
- Fixed a bug that caused the application to crash when songs were currently 
  added and the application got closed.

# v1.4.0

## Features
- The first element in the artist list is now always "All Artists", that can 
  be selected to display all artists.
- Each artist now displays the number of albums and songs underneath.
- Each playlist now displays the number of songs underneath its name.

## Bugfixes
- Fixed a bug that caused the YouTube views to be sorted wrong.
- Fixed a bug that caused the path of local songs to not be displayed.
- Fixed a bug that caused YouTube streaming and downloading to fail randomly.

# v1.3.0

## Features
- YouTube streaming doesn't require VLC media player to be installed anymore.
- The views of a YouTube video are now displayed.

## Changes
- The option "Remove from library and playlist" is removed. The option 
  "Remove from library" now also removes the songs from the playlist.
- The playlist timeout is now hidden when in party mode.
- "A" and "The" in the first word of an artist name are now ignored in the 
  ordered artist list.
  
## Improvements
- If the internet connection is slow, the application startup is not longer 
  slowed down.
  
## Bugfixes
- When the last song of a playlist has ended, the play button isn't clickable 
  anymore.

# v1.2.4

## Bugfixes
- Fixed a bug that caused the YouTube streaming to not work anymore.

# v1.2.3

## Bugfixes
- Fixed a bug that caused the sorting of songs to not work anymore.

# v1.2.2

## Bugfixes
- Fixed a bug that caused the application appearance related settings to be 
  overridden every time the application starts.

# v1.2.1

## Features
- The volume is now saved when closing the application.

## Bugfixes
- Fixed a bug that caused the application to crash, when the user paused a 
  song, switched to another playlist and then continued the song.
- Fixed application crash when rapidly clicking the play/pause button.

# v1.2.0

## Features
- The songs and playlist informations are now stored, so that they don't get 
  lost when closing the application.
- Added a link to report a bug
- Songs that can't be played are now marked as corrupt.

## Bugfixes
- Fixed a bug that caused the application to crash or start/pause a song when 
  editing the name of a playlist.
- Fixed a bug that caused the application to crash when playing a song from 
  YouTube.

# v1.1.0

## Features
- Multiple playlists.
- Shuffle playlists.

## Changes
- Increased the number of songs that can cache at one time from three to 10.

## Bugfixes
- Fixed a bug that causes the application to hang sometimes when adding songs 
  to the library.
- Fixed a bug that caused the sorting order of the YouTube rating not to 
  change.
- Fixed a bug that caused the volume to lock, even if the user was in 
  administrator mode.
- Fixed a bug that caused the time to lock, even if the user was in 
  administrator mode.

# v1.0.1

## Changes
- UI style changes
