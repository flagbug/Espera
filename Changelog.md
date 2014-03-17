# v2.0.0 Beta 1

## Features
- Espera has been completely rewritten
- Double clicking a song now starts the playback instantly, 
  there's no need to create a new playlist beforehand.
- New songs in the library folder and existing ones are automatically updates 
  if their metatdata has changed.
- The YouTube player can now show the video and has support for multiple 
  quality settings.
- Added support for AAC playback.
- Added a completely new update system that applies updates in the background.
- Album covers are now displayed next to the artist.
- Added a slider to change the scaling of the application. This is useful when 
  Espera is displayed on a TV screen.
- Espera can now be remote-controled via an Android app. The app will soon be 
  released.
- Added support for downloadig YouTube videos directly.

## Changes
- Youtube streaming is now enabled by default.
- Espera now requires .NET 4.5 to be installed.

# v1.7.6

## Changes
- YouTube streaming is now enabled by default.

## Bugfixes
- Fixed the settings not beeing fully accessible in a small window.

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
