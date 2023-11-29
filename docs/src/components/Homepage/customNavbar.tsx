import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import React from 'react';
import ThemedImage from '@theme/ThemedImage';
import { useColorMode } from "@docusaurus/theme-common"
import NavbarColorModeToggle from '@theme/Navbar/ColorModeToggle';

export default function CustomNavbar(): JSX.Element {

    return (
        <nav style={{ height: '60px', position: 'fixed', display: 'inline-flex', textAlign: 'center', alignItems: 'center', width: '100%' }} className='myNavbar'>
            <div style={{ display: 'flex', justifyContent: 'flex-start', width: '100%' }} className='margin-left--lg'>
                <Link
                    style={{ fontWeight: 'bold', fontFamily: 'RobotoMono' }}
                    className='margin-right--lg'
                    to="/docs/intro">
                    documentation
                </Link>
                <Link
                    style={{ fontWeight: 'bold', fontFamily: 'RobotoMono' }}
                    to="https://ertis.uma.es/">
                    about us
                </Link>
            </div>
            <div style={{ display: 'flex', justifyContent: 'center', height: '45px', width: '100%' }}>
                <img src={useBaseUrl('/img/logo.svg')} alt="opentwins logo" style={{ position: 'absolute', margin: 'auto', height: '45px', width: '45px' }} />
            </div>
            <div className='margin-right--lg' style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', width: '100%' }}>
                <Link
                    style={{ height: '24px' }}
                    className='margin-right--md'
                    to="https://github.com/ertis-research/OpenTwins">
                    <ThemedImage
                        alt="github logo"
                        height='24px'
                        sources={{
                            light: useBaseUrl('/img/github_black.svg'),
                            dark: useBaseUrl('/img/github_white.svg'),
                        }}
                    />
                </Link>
                <NavbarColorModeToggle />
            </div>
        </nav>
    );
}