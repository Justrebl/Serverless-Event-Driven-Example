const { exec } = require('child_process');
const path = require('path');
const config = require('./msEntraSampleConfig.json');

// Read the UserFlowDelay from the args.
// @see launch.json
const startDelay = Math.max(0, Number(process.argv[2] || 0) - Date.now());

if (startDelay > 0) {
  console.log(`Waiting for the user flow setup to finish before launching sample...`);
}

setTimeout(() => {
  console.log('Running Sample...');
  console.log('This may take some time.');

  exec(config.command, { cwd: path.resolve(config.workingDir) }, (err, output) => {
    if (err) {
      console.error(err);
      return;
    }
    console.log(output);
  });
}, startDelay);
