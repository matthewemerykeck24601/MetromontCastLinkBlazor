let viewer;
let viewerDocument;

window.initializeForgeViewer = function (containerId, accessToken) {
    const options = {
        env: 'AutodeskProduction',
        api: 'derivativeV2',
        accessToken: accessToken
    };

    Autodesk.Viewing.Initializer(options, () => {
        const container = document.getElementById(containerId);
        viewer = new Autodesk.Viewing.GuiViewer3D(container);
        viewer.start();
    });
};

window.loadModel = function (urn) {
    Autodesk.Viewing.Document.load(
        `urn:${urn}`,
        onDocumentLoadSuccess,
        onDocumentLoadFailure
    );
};

// Add other viewer functions...