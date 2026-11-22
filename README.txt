FrilledsAnnouncementSystemUI (prototype)

Files:
- announcement.html: responsive account UI and admin announcement composer
- server.js: lightweight Node backend for account registration/login and announcements
- FrilledsAnnouncementSystemUI.cs: Unity client polling receiver and smooth camera-follow display

Backend setup (Node 18+):
1. Install Node.js.
2. In this folder run: ADMIN_KEY=your-long-secret node server.js
   Windows PowerShell: $env:ADMIN_KEY='your-long-secret'; node server.js
3. The server is localhost:3000 by default. For production, deploy behind HTTPS, use a persistent database,
   implement abuse controls/moderation, and restrict CORS. This prototype uses in-memory storage and loses
   accounts/messages on restart. Do not use it as a production service.
4. Open announcement.html in a browser. Backend URL defaults to http://localhost:3000.
5. Create an account. Enter the ADMIN_KEY during registration to make that account an admin.
   Keep the key private. Only admin accounts can send announcements.

Unity setup:
1. Put FrilledsAnnouncementSystemUI.cs in Assets.
2. Add it to the display object. Set targetCamera to the VR camera transform.
3. Set offset/followSmoothness and optionally assign a world-space Canvas/panel and TMP or legacy text.
4. Set backendUrl to your deployed backend URL (or localhost for local PC testing).
5. Panel is expected to be a world-space display/panel assigned by you; this script updates references but does not create the Canvas.

Notes:
- The receiver polls public announcement data and does not require a Photon room.
- The HTML file is only a front end; server.js must be running and reachable for accounts/broadcasts.
- Prototype lacks database persistence, email verification, robust rate limits, moderation, HTTPS deployment,
  and production-grade session expiry. Add these before exposing it publicly.
