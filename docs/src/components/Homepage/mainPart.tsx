import React from 'react';
import ThemedImage from '@theme/ThemedImage';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Link from '@docusaurus/Link';

export default function CustomNavbar(): JSX.Element {
    return (
        <div style={{ width: '100%', margin: 0, padding: 0 }}>
            <div className="row" style={{ width: '100%', margin: 0, padding: 0 }}>
                <div className='col col--5' style={{ width: '100%', margin: 0, height: '100vh', paddingLeft: 0, display: 'flex', alignItems: 'center' }}>
                    <ThemedImage
                        className='round-border'
                        alt="Docusaurus themed image"
                        style={{ height: '80%', width: '100%', objectFit: 'cover', objectPosition: 'right' }}
                        sources={{
                            light: useBaseUrl('/img/captura-dark.png'),
                            dark: useBaseUrl('/img/captura-light.png'),
                        }}
                    />
                </div>
                <div className='col col--7' style={{ width: '100%', margin: 0, height: '100vh' }}>
                    <div style={{ height: '100%', margin: 'auto', display: 'grid', alignContent: 'center' }}>
                        <div className="wrapper">
                            <div className="typing-demo">
                                <span className='mainTitle'>opentwins</span>
                            </div>
                        </div>
                        <p className='text--center margin-top--lg padding-horiz--xl' style={{ fontWeight: 'normal', fontFamily: 'RobotoMono' }}>
                            Innovative <u>open-source</u> platform that specializes in <br /> developing next-gen compositional <u>digital twins</u>
                        </p>

                        <div className='center-content margin-top--lg margin-bottom--md'>
                            <Link
                                className="button button--primary button--lg"
                                to="/docs/intro">
                                Get started
                            </Link>
                            <Link
                                className="button button--secondary button--lg margin-left--md"
                                to="https://github.com/ertis-research/OpenTwins">
                                GitHub
                            </Link>
                        </div>

                        <div className='center-content margin-top--lg' style={{ height: '40px' }}>
                            <ThemedImage
                                alt="ertis logo"
                                sources={{
                                    light: useBaseUrl('/img/ertis_black.svg'),
                                    dark: useBaseUrl('/img/ertis_white.svg'),
                                }}
                            />
                            <ThemedImage
                                alt="itis logo"
                                className='margin-left--md'
                                sources={{
                                    light: useBaseUrl('/img/ITIS_black.svg'),
                                    dark: useBaseUrl('/img/ITIS_white.svg'),
                                }}
                            />
                            <ThemedImage
                                alt="uma logo"
                                className='margin-left--md'
                                sources={{
                                    light: useBaseUrl('/img/uma_black.png'),
                                    dark: useBaseUrl('/img/uma_white.png'),
                                }}
                            />
                        </div>

                    </div>
                </div>
            </div>
        </div>
    );
}