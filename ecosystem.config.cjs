module.exports = {
    apps: [{
        name: 'GATool',
        script: 'app.js',
        env: {
            'PORT': 8080,
            'NODE_ENV': 'production'
        }
    }],

    // Deployment Configuration
    deploy: {
        production: {
            "user": "gatool",
            "host": ["gatool-prod-a", "gatool-prod-b", "gatool-prod-c"],
            "ref": "origin/main",
            "repo": "https://github.com/arthurlockman/gatool-api",
            "path": "/home/gatool/pm2/gatool",
            "post-deploy": "cp ~/gatool.env .env && npm ci && npm run write-version && pm2 startOrRestart ecosystem.config.cjs --env production && pm2 save"
        }
    }
}