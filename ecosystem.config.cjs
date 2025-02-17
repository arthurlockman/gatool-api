module.exports = {
  apps: [
    {
      name: 'GATool',
      script: "npm",
      args: "run start",
      env: {
        PORT: 8080,
        NODE_ENV: 'production'
      }
    }
  ],

  // Deployment Configuration
  deploy: {
    production: {
      user: 'gatool',
      host: ['gatool-prod-a', 'gatool-prod-b', '100.126.229.79'],
      ref: 'origin/main',
      repo: 'https://github.com/arthurlockman/gatool-api',
      path: '/home/gatool/pm2/gatool',
      'post-deploy':
        'cp ~/gatool.env .env && rm -rf ./node_modules && npm i && npm run write-version && pm2 startOrRestart ecosystem.config.cjs --env production && pm2 save'
    }
  }
};
