window.pcNotifications = window.pcNotifications || {
    play: function (kind) {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;

            const context = window.pcNotifications._context || (window.pcNotifications._context = new AudioContext());
            const start = () => {
                const now = context.currentTime;
                const notes = kind === 'error' || kind === 'warning'
                    ? [{ frequency: 220, at: 0, length: 0.12 }, { frequency: 165, at: 0.14, length: 0.16 }]
                    : [{ frequency: 523.25, at: 0, length: 0.11 }, { frequency: 659.25, at: 0.12, length: 0.18 }];

                notes.forEach(note => {
                    const oscillator = context.createOscillator();
                    const gain = context.createGain();
                    oscillator.type = kind === 'success' ? 'sine' : 'triangle';
                    oscillator.frequency.setValueAtTime(note.frequency, now + note.at);
                    gain.gain.setValueAtTime(0.0001, now + note.at);
                    gain.gain.exponentialRampToValueAtTime(0.045, now + note.at + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, now + note.at + note.length);
                    oscillator.connect(gain);
                    gain.connect(context.destination);
                    oscillator.start(now + note.at);
                    oscillator.stop(now + note.at + note.length + 0.02);
                });
            };

            if (context.state === 'suspended') {
                context.resume().then(start).catch(() => { });
            } else {
                start();
            }
        } catch (_) {
            // Sound is an enhancement. A blocked audio device must not break the UI.
        }
    }
};
