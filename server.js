const http = require("http");
const crypto = require("crypto");

const PORT = process.env.PORT || 3000;
const ADMIN_KEY = process.env.ADMIN_KEY || "";

const users = [];
const tokens = new Map();
const announcements = [];

function send(res, status, data) {
    res.writeHead(status, {
        "Content-Type": "application/json",
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Headers": "Content-Type, Authorization",
        "Access-Control-Allow-Methods": "GET, POST, OPTIONS"
    });
    res.end(JSON.stringify(data));
}

function hashPassword(password, salt) {
    return crypto.pbkdf2Sync(password, salt, 100000, 64, "sha512").toString("hex");
}

function getBody(req) {
    return new Promise((resolve, reject) => {
        let body = "";

        req.on("data", chunk => {
            body += chunk;
            if (body.length > 1e6) {
                req.destroy();
                reject(new Error("Request too large"));
            }
        });

        req.on("end", () => {
            try {
                resolve(JSON.parse(body || "{}"));
            } catch {
                reject(new Error("Invalid JSON"));
            }
        });

        req.on("error", reject);
    });
}

function getUser(req) {
    const header = req.headers.authorization || "";
    const token = header.startsWith("Bearer ") ? header.slice(7) : "";
    const username = tokens.get(token);

    if (!username) return null;
    return users.find(u => u.username === username) || null;
}

function createToken(username) {
    const token = crypto.randomBytes(32).toString("hex");
    tokens.set(token, username);
    return token;
}

const server = http.createServer(async (req, res) => {
    if (req.method === "OPTIONS") {
        return send(res, 200, {});
    }

    const path = req.url.split("?")[0];

    try {
        // REGISTER
        if (path === "/register" && req.method === "POST") {
            const b = await getBody(req);

            if (!b.username || !b.password) {
                return send(res, 400, { error: "Username and password required" });
            }

            if (String(b.username).length > 32 || String(b.password).length > 256) {
                return send(res, 400, { error: "Username or password too long" });
            }

            if (users.some(u => u.username === b.username)) {
                return send(res, 400, { error: "Username already exists" });
            }

            const isAdmin = !!ADMIN_KEY && b.adminKey === ADMIN_KEY;
            const salt = crypto.randomBytes(16).toString("hex");

            const user = {
                username: String(b.username),
                salt,
                passwordHash: hashPassword(String(b.password), salt),
                admin: isAdmin
            };

            users.push(user);

            const token = createToken(user.username);

            return send(res, 200, {
                token,
                admin: user.admin,
                username: user.username
            });
        }

        // LOGIN
        if (path === "/login" && req.method === "POST") {
            const b = await getBody(req);

            const user = users.find(u => u.username === b.username);

            if (!user || hashPassword(String(b.password || ""), user.salt) !== user.passwordHash) {
                return send(res, 401, { error: "Invalid username or password" });
            }

            const token = createToken(user.username);

            return send(res, 200, {
                token,
                admin: user.admin,
                username: user.username
            });
        }

        // POST ANNOUNCEMENT
        if (path === "/announce" && req.method === "POST") {
            const user = getUser(req);

            if (!user) {
                return send(res, 401, { error: "Not authenticated" });
            }

            if (!user.admin) {
                return send(res, 403, { error: "Admin access required" });
            }

            const b = await getBody(req);

            if (!b.message || !String(b.message).trim()) {
                return send(res, 400, { error: "Message cannot be empty" });
            }

            if (String(b.message).length > 500) {
                return send(res, 400, { error: "Message exceeds 500 characters" });
            }

            const announcement = {
                id: crypto.randomBytes(8).toString("hex"),
                sender: user.username,
                message: String(b.message).trim(),
                createdAt: Date.now()
            };

            announcements.push(announcement);

            return send(res, 200, {
                success: true,
                announcement
            });
        }

        // GET ANNOUNCEMENTS
        if (path === "/announcements" && req.method === "GET") {
            return send(res, 200, {
                items: [...announcements].reverse()
            });
        }

        // LOGOUT
        if (path === "/logout" && req.method === "POST") {
            const header = req.headers.authorization || "";
            const token = header.startsWith("Bearer ") ? header.slice(7) : "";

            tokens.delete(token);

            return send(res, 200, { success: true });
        }

        return send(res, 404, { error: "Route not found" });

    } catch (err) {
        console.error(err);
        return send(res, 500, { error: "Internal server error" });
    }
});

server.listen(PORT, "0.0.0.0", () => {
    console.log(`Frilleds announcement server running on port ${PORT}`);
});
